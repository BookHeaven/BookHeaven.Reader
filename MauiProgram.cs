using BlazorPanzoom;
using BookHeaven.Core;
using BookHeaven.Core.Abstractions;
using CommunityToolkit.Maui;
using BookHeaven.EbookManager;
using BookHeaven.Reader.Components.Dialogs.Books;
using BookHeaven.Reader.Components.Shared.ContextMenu;
using BookHeaven.Reader.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;

#if ANDROID
using Microsoft.Maui.Hosting;
#endif

namespace BookHeaven.Reader;

public static class MauiProgram
{

	internal static readonly IReadOnlyList<string> Assets = [
		"/cache",
		"/covers",
		"/books",
		"/fonts"
	];

	public static MauiApp CreateMauiApp()
	{

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit();

		builder.ConfigureMauiHandlers(handlers =>
		{
#if ANDROID || WINDOWS
			handlers.AddHandler<BlazorWebView, BookHeavenWebViewHandler>();
#endif
		});

		builder.Services.AddCore(options =>
		{
			options.BooksPath = Path.Combine(FileSystem.AppDataDirectory, "books");
			options.CoversPath = Path.Combine(FileSystem.AppDataDirectory, "covers");
			options.FontsPath = Path.Combine(FileSystem.AppDataDirectory, "fonts");
			options.DatabasePath = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
			options.CachePath = Path.Combine(FileSystem.CacheDirectory, "cache");
			options.DefaultFontDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "ui-font", "Montserrat");
		});

		builder.Services.AddSingleton<AppStateService>();
		builder.Services.AddSingleton<LifeCycleService>();
		builder.Services.AddSingleton<UdpBroadcastClient>();
		builder.Services.AddSingleton<IAppsService, AppsService>();
		builder.Services.AddSingleton<BookInfoDialogService>();

		builder.Services.AddScoped<ImageViewerService>();
		builder.Services.AddScoped<OverlayService>();
		builder.Services.AddScoped<IContextMenuService, ContextMenuService>();

		builder.Services.AddTransient<IServerService, ServerService>();
		builder.Services.AddTransient<IAlertService, AlertService>();



		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddBlazorPanzoomServices();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var appStateService = scope.ServiceProvider.GetRequiredService<AppStateService>();

			if (appStateService.DeviceId == Guid.Empty)
			{
				appStateService.DeviceId = Guid.NewGuid();
			}
		}

		app.Services.ApplyDatabaseMigrations();

		return app;
	}
}