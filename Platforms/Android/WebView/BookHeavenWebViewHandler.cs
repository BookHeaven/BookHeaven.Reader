using System.Text;
using Android.Graphics;
using Android.Webkit;
using BookHeaven.EbookManager;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Path = System.IO.Path;

namespace BookHeaven.Reader.WebView;

public class BookHeavenWebViewHandler : BlazorWebViewHandler
{
	protected override void ConnectHandler(Android.Webkit.WebView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SetWebViewClient(new BookHeavenWebViewClient(platformView.WebViewClient));
	}
}

public class BookHeavenWebViewClient(WebViewClient? innerClient = null) : WebViewClient
{
	private static readonly Dictionary<string, string> CacheHeaders = new()
	{
		["Cache-Control"] = "public, max-age=31536000, immutable"
	};

	private static readonly Dictionary<string, string> NotFoundHeaders = new()
	{
		["Cache-Control"] = "no-store"
	};

	public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		return innerClient?.ShouldOverrideUrlLoading(view, request) ?? base.ShouldOverrideUrlLoading(view, request);
	}

	public override void OnPageStarted(Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		innerClient?.OnPageStarted(view, url, favicon);
		base.OnPageStarted(view, url, favicon);
	}

	public override void OnPageFinished(Android.Webkit.WebView? view, string? url)
	{
		innerClient?.OnPageFinished(view, url);
		base.OnPageFinished(view, url);
	}

	public override WebResourceResponse? ShouldInterceptRequest(Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		if(request?.Url is null || request.Url.Host != "0.0.0.1" || MauiProgram.Assets.All(folder => !request.Url.Path?.StartsWith(folder, StringComparison.OrdinalIgnoreCase) == true))
		{
			return innerClient?.ShouldInterceptRequest(view, request) ?? base.ShouldInterceptRequest(view, request);
		}

		return HandleBookHeavenRequest(new Uri(request.Url.ToString()!));
	}

	private static WebResourceResponse HandleBookHeavenRequest(Uri requestUrl)
	{
		var filePath = requestUrl.AbsolutePath.TrimStart('/');
		var queryIndex = filePath.IndexOf('?');
		if (queryIndex >= 0)
		{
			filePath = filePath[..queryIndex];
		}

		if (filePath.Contains("..", StringComparison.Ordinal))
		{
			return CreateNotFoundResponse();
		}
		
		var absolutePath = Path.Combine(FileSystem.AppDataDirectory, filePath);
		if (filePath.StartsWith("cache"))
		{
			absolutePath = Path.Combine(FileSystem.CacheDirectory, filePath);
		}
		
		if (!File.Exists(absolutePath))
		{
			return CreateNotFoundResponse();
		}

		var contentType = GetContentType(absolutePath);
		var fileBytes = File.ReadAllBytes(absolutePath);
		return new WebResourceResponse(contentType, null, 200, "OK", new Dictionary<string, string>(CacheHeaders), new MemoryStream(fileBytes));
	}

	private static WebResourceResponse CreateNotFoundResponse()
	{
		const string payload = "Resource not found.";
		return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", new Dictionary<string, string>(NotFoundHeaders), new MemoryStream(Encoding.UTF8.GetBytes(payload)));
	}

	private static string GetContentType(string filePath)
	{
		return Path.GetExtension(filePath).ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".webp" => "image/webp",
			".bmp" => "image/bmp",
			".svg" => "image/svg+xml",
			_ => "application/octet-stream"
		};
	}
}
