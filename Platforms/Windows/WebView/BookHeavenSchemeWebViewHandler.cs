using BookHeaven.Core;
using BookHeaven.EbookManager;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace BookHeaven.Reader.WebView;

public class BookHeavenSchemeWebViewHandler : BlazorWebViewHandler
{
	protected override void ConnectHandler(WebView2 platformView)
	{
		base.ConnectHandler(platformView);
		platformView.CoreWebView2Initialized += (_, _) => RegisterCustomScheme(platformView);
		if (platformView.CoreWebView2 is not null)
		{
			RegisterCustomScheme(platformView);
		}
	}

	private static void RegisterCustomScheme(WebView2 platformView)
	{
		if (platformView.CoreWebView2 is null)
		{
			return;
		}

		platformView.CoreWebView2.AddWebResourceRequestedFilter($"{BookHeavenScheme.Scheme}*", CoreWebView2WebResourceContext.Image);
		platformView.CoreWebView2.WebResourceRequested += (_, args) =>
		{
			var requestedUri = args.Request.Uri;
			if (!requestedUri.StartsWith(BookHeavenScheme.Scheme, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			
			var uri = new Uri(requestedUri);
			var resourceFolder = uri.Host;
			var filePath = uri.AbsolutePath.TrimStart('/');
			if (filePath.StartsWith("..", StringComparison.Ordinal))
			{
				return;
			}

			var absolutePath = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, resourceFolder, filePath));
			if (resourceFolder == "cache")
			{
				absolutePath = Path.Combine(FileSystem.CacheDirectory, resourceFolder, filePath);
			}

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
			var contentType = extension switch
			{
				".jpg" or ".jpeg" => "image/jpeg",
				".png" => "image/png",
				".gif" => "image/gif",
				".webp" => "image/webp",
				".bmp" => "image/bmp",
				".svg" => "image/svg+xml",
				_ => "application/octet-stream"
			};

			using var fileStream = File.OpenRead(absolutePath);
			var bytes = File.ReadAllBytes(absolutePath);
			args.Response = platformView.CoreWebView2.Environment.CreateWebResourceResponse(
				new MemoryStream(bytes).AsRandomAccessStream(),
				200,
				"OK",
				$"Content-Type: {contentType}\r\nCache-Control: public, max-age=31536000, immutable");
		};
	}
}