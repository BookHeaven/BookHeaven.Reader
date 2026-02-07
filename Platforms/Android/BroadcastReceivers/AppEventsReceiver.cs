using Android.App;
using Android.Content;
using BookHeaven.Reader.Interfaces;

namespace BookHeaven.Reader.BroadcastReceivers;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([Intent.ActionPackageRemoved, Intent.ActionPackageFullyRemoved, Intent.ActionPackageAdded], DataScheme = "package")]
public class AppEventsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var appsService = IPlatformApplication.Current?.Services.GetService<IAppsService>();
        if (appsService != null)
        {
            _ = appsService.RefreshInstalledAppsAsync(); // Ejecutar asíncrono, no bloquear
        }
    }
}