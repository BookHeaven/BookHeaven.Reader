using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using MimeTypes;

namespace BookHeaven.Reader.WebView;

public class BookHeavenWebViewHandler : BlazorWebViewHandler
{
	protected override void ConnectHandler(WebView2 platformView)
	{
		base.ConnectHandler(platformView);
		platformView.CoreWebView2Initialized += (_, _) => RegisterWebViewHandler(platformView);
		if (platformView.CoreWebView2 is not null)
		{
			RegisterWebViewHandler(platformView);
		}
	}

	private static void RegisterWebViewHandler(WebView2 platformView)
	{
		if (platformView.CoreWebView2 is null)
		{
			return;
		}
		
		platformView.CoreWebView2.WebResourceRequested += (_, args) =>
		{
			var requestedUri = new Uri(args.Request.Uri);
			// Only handle requests targeting the configured local host to avoid serving other hosts
			if (!string.Equals(requestedUri.Host, "0.0.0.1", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			if (!MauiProgram.Assets.Any(folder => requestedUri.AbsolutePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
			{
				return;
			}
			
			var filePath = requestedUri.AbsolutePath.TrimStart('/');
			if (filePath.StartsWith("..", StringComparison.Ordinal))
			{
				return;
			}

			var basePath = filePath.StartsWith("cache", StringComparison.OrdinalIgnoreCase)
				? FileSystem.CacheDirectory
				: FileSystem.AppDataDirectory;
			
			var absolutePath = Path.GetFullPath(Path.Combine(basePath, filePath));

			if (!File.Exists(absolutePath))
			{
				args.Response = platformView.CoreWebView2.Environment.CreateWebResourceResponse(
					new MemoryStream([.. "Image not found."u8]).AsRandomAccessStream(),
					404,
					"Not Found",
					"Content-Type: text/plain;charset=utf-8\r\nCache-Control: no-store");
				return;
			}

			var extension = Path.GetExtension(absolutePath).ToLowerInvariant();

			using var fileStream = File.OpenRead(absolutePath);
			var bytes = File.ReadAllBytes(absolutePath);
			args.Response = platformView.CoreWebView2.Environment.CreateWebResourceResponse(
				new MemoryStream(bytes).AsRandomAccessStream(),
				200,
				"OK",
				$"Content-Type: {MimeTypeMap.GetMimeType(extension)}\r\nCache-Control: public, max-age=31536000, immutable");
		};
	}
}