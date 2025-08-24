using Android.App;
using Android.Content;
using BookHeaven.Reader.Services;

namespace BookHeaven.Reader.BroadcastReceivers;

// Unused, but could be useful in the future
[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter([Intent.ActionScreenOn, Intent.ActionScreenOff])]
public class ScreenEventsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var lifeCycleService = IPlatformApplication.Current!.Services.GetService<LifeCycleService>();
        if (lifeCycleService == null || intent == null) return;
        switch (intent.Action)
        {
            case Intent.ActionScreenOn:
                lifeCycleService.ScreenOn?.Invoke();
                break;
            case Intent.ActionScreenOff:
                lifeCycleService.ScreenOff?.Invoke();
                break;
        }
    }
}