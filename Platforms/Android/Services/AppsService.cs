using Android.Content;
using Android.Content.PM;
using Android.Provider;
using BookHeaven.Reader.Interfaces;
using AppInfo = BookHeaven.Reader.Entities.AppInfo;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace BookHeaven.Reader.Services;

public class AppsService : IAppsService
{
	public void OpenApp(string packageName)
	{
		var intent = Application.Context.PackageManager!.GetLaunchIntentForPackage(packageName);
		if (intent == null) return;
		
		intent.AddFlags(ActivityFlags.NewTask);
		Application.Context.StartActivity(intent);
	}

	public void OpenInfo(string packageName)
	{
		Intent intent = new(Settings.ActionApplicationDetailsSettings);
		var uri = Uri.FromParts("package", packageName, null);
		if (uri == null)
		{
			return;
		}
		intent.SetData(uri);
		intent.AddFlags(ActivityFlags.NewTask);
		Application.Context.StartActivity(intent);
	}

	public bool CanBeUninstalled(string packageName)
	{
		var packageManager = Application.Context.PackageManager!;
		try
		{
			var appInfo = packageManager.GetApplicationInfo(packageName, 0);
			return !appInfo.Flags.HasFlag(ApplicationInfoFlags.System) && !appInfo.Flags.HasFlag(ApplicationInfoFlags.UpdatedSystemApp);
		}
		catch (PackageManager.NameNotFoundException)
		{
			return false;
		}
	}

	public void UninstallApp(string packageName)
	{
		Intent intent = new(Intent.ActionDelete);
		var uri = Uri.FromParts("package", packageName, null);
		if (uri == null) return;
		
		intent.SetData(uri);
		intent.AddFlags(ActivityFlags.NewTask);
		Application.Context.StartActivity(intent);
	}

	public List<AppInfo> GetInstalledApps()
	{
		List<AppInfo> installedApps = [];

		var packageManager = Application.Context.PackageManager!;

		var apps = packageManager.QueryIntentActivities(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher), 0);
		foreach (var app in apps)
		{

			if (app.ActivityInfo?.IsEnabled == false) continue;
			
			var packageInfo = packageManager.GetPackageInfo(app.ActivityInfo?.PackageName!, 0)!;

			installedApps.Add(new AppInfo
			{
				Name = app.LoadLabel(packageManager),
				PackageName = app.ActivityInfo?.PackageName,
				IconBase64 = Helpers.ConvertDrawableToBase64(app.LoadIcon(packageManager)!),
				FirstInstallTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(packageInfo.FirstInstallTime)
			});
		}

		return installedApps;
	}
}