using System.Runtime.Versioning;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using BookHeaven.Reader.Interfaces;
using AppInfo = BookHeaven.Reader.Entities.AppInfo;
using Uri = Android.Net.Uri;

namespace BookHeaven.Reader.Services
{
	public class AppsService : IAppsService
	{
		public void OpenApp(string packageName)
		{
			Intent? intent = Android.App.Application.Context.PackageManager!.GetLaunchIntentForPackage(packageName);
			if (intent != null)
			{
				intent.AddFlags(ActivityFlags.NewTask);
				Android.App.Application.Context.StartActivity(intent);
			}
		}

		public void OpenInfo(string packageName)
		{
			Intent intent = new(Android.Provider.Settings.ActionApplicationDetailsSettings);
			Uri? uri = Uri.FromParts("package", packageName, null);
			if (uri == null)
			{
				return;
			}
			intent.SetData(uri);
			intent.AddFlags(ActivityFlags.NewTask);
			Android.App.Application.Context.StartActivity(intent);
		}

		public bool CanBeUninstalled(string packageName)
		{
			PackageManager packageManager = Android.App.Application.Context.PackageManager!;
			try
			{
				ApplicationInfo appInfo = packageManager.GetApplicationInfo(packageName, 0);
				if(appInfo.Flags.HasFlag(ApplicationInfoFlags.System) || appInfo.Flags.HasFlag(ApplicationInfoFlags.UpdatedSystemApp)) {
					return false;
				}
				return true;
			}
			catch (PackageManager.NameNotFoundException)
			{
				return false;
			}
		}

		public void UninstallApp(string packageName)
		{
			Intent intent = new(Intent.ActionDelete);
			Uri? uri = Uri.FromParts("package", packageName, null);
			if (uri == null)
			{
				return;
			}
			intent.SetData(uri);
			intent.AddFlags(ActivityFlags.NewTask);
			Android.App.Application.Context.StartActivity(intent);
		}

		public List<AppInfo> GetInstalledApps()
		{
			List<AppInfo> installedApps = [];

			PackageManager packageManager = Android.App.Application.Context.PackageManager!;

			IList<ResolveInfo> apps = packageManager.QueryIntentActivities(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher), 0);
			foreach (var app in apps)
			{

				if (app.ActivityInfo?.IsEnabled == false)
				{
					continue;
				}
				PackageInfo packageInfo = packageManager.GetPackageInfo(app.ActivityInfo?.PackageName!, 0)!;

				installedApps.Add(new AppInfo
				{
					Name = app.LoadLabel(packageManager)?.ToString(),
					PackageName = app.ActivityInfo?.PackageName,
					IconBase64 = Helpers.ConvertDrawableToBase64(app.LoadIcon(packageManager)!),
					FirstInstallTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(packageInfo.FirstInstallTime)
				});
			}

			return installedApps;
		}
	}
}

