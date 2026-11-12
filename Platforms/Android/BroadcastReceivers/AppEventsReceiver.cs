using Android.App;
using Android.Content;
using BookHeaven.Reader.Features.AppDrawer.Abstractions;

namespace BookHeaven.Reader.BroadcastReceivers;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([Intent.ActionPackageRemoved, Intent.ActionPackageFullyRemoved, Intent.ActionPackageAdded], DataScheme = "package")]
public sealed class AppEventsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var appsService = IPlatformApplication.Current?.Services.GetService<IAppDrawerService>();
        if (appsService != null)
        {
            _ = appsService.RefreshInstalledAppsAsync(); // Ejecutar asíncrono, no bloquear
        }
    }
}