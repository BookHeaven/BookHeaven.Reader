using BlazorPanzoom;
using BookHeaven.Domain;
using BookHeaven.Domain.Abstractions;
using BookHeaven.Reader.Interfaces;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui;
using BookHeaven.EpubManager;
using Microsoft.Extensions.Logging;

namespace BookHeaven.Reader;

public static class MauiProgram
{
	public static readonly string BooksPath = Path.Combine(FileSystem.AppDataDirectory, "books");
	public static readonly string CoversPath = Path.Combine(FileSystem.AppDataDirectory, "covers");
	public static readonly string CachePath = Path.Combine(FileSystem.AppDataDirectory, "cache");
	public static readonly string FontsPath = Path.Combine(FileSystem.AppDataDirectory, "fonts");
		
	public static MauiApp CreateMauiApp()
	{
		Directory.CreateDirectory(BooksPath);
		Directory.CreateDirectory(CoversPath);
		Directory.CreateDirectory(CachePath);
		Directory.CreateDirectory(FontsPath);

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit();

		builder.Services.AddDomain(BooksPath, CoversPath, FontsPath, FileSystem.AppDataDirectory);
		builder.Services.AddEpubManager();
			
			
		builder.Services.AddSingleton<AppStateService>();
		builder.Services.AddSingleton<LifeCycleService>();
		builder.Services.AddSingleton<UdpBroadcastClient>();
		builder.Services.AddSingleton<IAppsService, AppsService>();
		
		builder.Services.AddScoped<ReaderService>();
		builder.Services.AddScoped<ProfileSettingsService>();
		builder.Services.AddScoped<ImageViewerService>();
		builder.Services.AddScoped<OverlayService>();
			
		builder.Services.AddTransient<IServerService, ServerService>();
		builder.Services.AddTransient<IAlertService, AlertService>();

		builder.Services.AddBlazorContextMenu();
		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddBlazorPanzoomServices();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}
}