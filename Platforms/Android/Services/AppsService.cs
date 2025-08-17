using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Android.OS;
using Android.Net;
using BookHeaven.Reader.Interfaces;
using BookHeaven.Reader.Entities;
using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Services;

public class AppsService : IAppsService
{
    public List<AppInfo> Apps { get; set; } = new List<AppInfo>();
    public Action? OnAppsChanged { get; set; }

    public void OpenApp(string packageName)
    {
        var intent = Android.App.Application.Context.PackageManager!.GetLaunchIntentForPackage(packageName);
        if (intent == null) return;
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
    }

    public void OpenInfo(string packageName)
    {
        Intent intent = new(Settings.ActionApplicationDetailsSettings);
        var uri = Android.Net.Uri.FromParts("package", packageName, null);
        if (uri == null) return;
        intent.SetData(uri);
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
    }

    public void OpenAppShortcut(string packageName, string shortcutId)
    {
        var launcherApps = (LauncherApps)Android.App.Application.Context.GetSystemService(Context.LauncherAppsService)!;
        var userHandle = Process.MyUserHandle();
        launcherApps.StartShortcut(packageName, shortcutId, null, null, userHandle);
    }

    public bool CanBeUninstalled(string packageName)
    {
        var packageManager = Android.App.Application.Context.PackageManager!;
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
        var uri = Android.Net.Uri.FromParts("package", packageName, null);
        if (uri == null) return;
        intent.SetData(uri);
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
    }

    public void RefreshInstalledApps()
    {
        var packageManager = Android.App.Application.Context.PackageManager!;
        var apps = packageManager.QueryIntentActivities(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher), 0);
        Apps.Clear();

        var appInfos = new List<AppInfo>(apps.Count);
        var launcherApps = Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop
            ? (LauncherApps)Android.App.Application.Context.GetSystemService(Context.LauncherAppsService)!
            : null;
        var userHandle = Process.MyUserHandle();

        Parallel.ForEach(apps, app =>
        {
            if (app.ActivityInfo?.IsEnabled == false) return;
            var packageInfo = packageManager.GetPackageInfo(app.ActivityInfo?.PackageName!, 0)!;
            var iconBase64 = Helpers.ConvertDrawableToBase64(app.LoadIcon(packageManager)!);
            var appInfo = new AppInfo
            {
                Name = app.LoadLabel(packageManager),
                PackageName = app.ActivityInfo?.PackageName,
                IconBase64 = iconBase64,
                FirstInstallTime = packageInfo.FirstInstallTime == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(packageInfo.FirstInstallTime).DateTime
            };

            try
            {
                // Get shortcuts using LauncherApps (API 25+)
                if (launcherApps != null && Build.VERSION.SdkInt >= BuildVersionCodes.NMr1)
                {
                    var shortcutQuery = new LauncherApps.ShortcutQuery();
                    shortcutQuery.SetPackage(app.ActivityInfo.PackageName);
                    shortcutQuery.SetQueryFlags(LauncherAppsShortcutQueryFlags.MatchDynamic | LauncherAppsShortcutQueryFlags.MatchPinned | LauncherAppsShortcutQueryFlags.MatchManifest);
                    var shortcuts = launcherApps.GetShortcuts(shortcutQuery, userHandle);
                    if (shortcuts != null)
                    {
                        foreach (var shortcut in shortcuts)
                        {
                            string? shortcutIconBase64 = null;
                            var iconDrawable = launcherApps.GetShortcutIconDrawable(shortcut, 0);
                            if (iconDrawable != null)
                            {
                                shortcutIconBase64 = Helpers.ConvertDrawableToBase64(iconDrawable);
                            }
                            appInfo.Shortcuts.Add(new AppShortcut
                            {
                                Id = shortcut.Id,
                                ShortLabel = shortcut.ShortLabel,
                                LongLabel = shortcut.LongLabel,
                                IconBase64 = shortcutIconBase64
                            });
                        }
                    }
                }
            }
            catch { }
            
            
            lock (appInfos)
            {
                appInfos.Add(appInfo);
            }
        });
        Apps.AddRange(appInfos);
        OnAppsChanged?.Invoke();
    }
}