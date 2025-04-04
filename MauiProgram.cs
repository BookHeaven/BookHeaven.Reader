using BlazorPanzoom;
using BookHeaven.Domain;
using BookHeaven.Reader.Interfaces;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui;
using EpubManager;
using Microsoft.Extensions.Logging;
using DependencyInjection = BookHeaven.Domain.DependencyInjection;

namespace BookHeaven.Reader
{
	public static class MauiProgram
	{
		public static readonly string BooksPath = Path.Combine(FileSystem.AppDataDirectory, "books");
		public static readonly string CoversPath = Path.Combine(FileSystem.AppDataDirectory, "covers");
		public static readonly string CachePath = Path.Combine(FileSystem.AppDataDirectory, "cache");
		
		public static MauiApp CreateMauiApp()
		{
            Directory.CreateDirectory(BooksPath);
			Directory.CreateDirectory(CoversPath);
			Directory.CreateDirectory(CachePath);

			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit();

			builder.Services.AddDomain(FileSystem.AppDataDirectory, DependencyInjection.DatabaseInjectionType.Service);
			builder.Services.AddEpubManager(true);
			
			
			builder.Services.AddSingleton<AppStateService>();
			builder.Services.AddSingleton<LifeCycleService>();

			builder.Services.AddScoped<BookManager>();
			builder.Services.AddScoped<IAppsService, AppsService>();
			builder.Services.AddScoped<ImageViewerService>();
			builder.Services.AddScoped<OverlayService>();
			
			builder.Services.AddTransient<IServerService, ServerService>();
			builder.Services.AddTransient<AlertService>();

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
}
