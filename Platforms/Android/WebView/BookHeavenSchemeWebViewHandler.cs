using System.Text;
using Android.Graphics;
using Android.Webkit;
using BookHeaven.EbookManager;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Path = System.IO.Path;

namespace BookHeaven.Reader.WebView;

public class BookHeavenSchemeWebViewHandler : BlazorWebViewHandler
{
	protected override void ConnectHandler(Android.Webkit.WebView platformView)
	{
		base.ConnectHandler(platformView);
		platformView.SetWebViewClient(new BookHeavenSchemeWebViewClient(platformView.WebViewClient));
	}
}

public class BookHeavenSchemeWebViewClient(WebViewClient? innerClient = null) : WebViewClient
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
		if (request?.Url != null && string.Equals(request.Url.Scheme, BookHeavenScheme.Scheme, StringComparison.OrdinalIgnoreCase))
		{
			var requestUrl = request.Url.ToString();
			if (!string.IsNullOrWhiteSpace(requestUrl) && requestUrl.StartsWith(BookHeavenScheme.Prefix, StringComparison.OrdinalIgnoreCase))
			{
				return HandleBookHeavenRequest(requestUrl);
			}
		}

		return innerClient?.ShouldInterceptRequest(view, request) ?? base.ShouldInterceptRequest(view, request);
	}

	private static WebResourceResponse HandleBookHeavenRequest(string requestUrl)
	{
		var uri = new Uri(requestUrl);
		var resourceFolder = uri.Host;
		var filePath = uri.AbsolutePath.TrimStart('/');
		var queryIndex = filePath.IndexOf('?');
		if (queryIndex >= 0)
		{
			filePath = filePath[..queryIndex];
		}

		if (filePath.Contains("..", StringComparison.Ordinal))
		{
			return CreateNotFoundResponse();
		}
		
		var absolutePath = Path.Combine(FileSystem.AppDataDirectory, resourceFolder, filePath);
		if (resourceFolder == "cache")
		{
			absolutePath = Path.Combine(FileSystem.CacheDirectory, resourceFolder, filePath);
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
