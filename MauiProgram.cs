using BlazorPanzoom;
using BookHeaven.Domain;
using BookHeaven.Domain.Abstractions;
using BookHeaven.Reader.Interfaces;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui;
using BookHeaven.EbookManager;
using Microsoft.Extensions.Logging;

namespace BookHeaven.Reader;

public static class MauiProgram
{
	public static readonly string BooksPath = Path.Combine(FileSystem.AppDataDirectory, "books");
	public static readonly string CoversPath = Path.Combine(FileSystem.AppDataDirectory, "covers");
	public static readonly string CachePath = Path.Combine(FileSystem.CacheDirectory, "cache");
	public static readonly string FontsPath = Path.Combine(FileSystem.AppDataDirectory, "fonts");
		
	public static MauiApp CreateMauiApp()
	{

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit();

		builder.Services.AddDomain(options =>
		{
			options.BooksPath = BooksPath;
			options.CoversPath = CoversPath;
			options.FontsPath = FontsPath;
			options.DatabasePath = FileSystem.AppDataDirectory;
		});
		builder.Services.AddEbookManager(options =>
		{
			options.CachePath = CachePath;
		});
		
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
#endif
		return builder.Build();
	}
}