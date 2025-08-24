using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using BookHeaven.Reader.BroadcastReceivers;

namespace BookHeaven.Reader.Services;

// Unused, but could be useful in the future
[Service(Name = "dev.ggarrido.bookheaven."+nameof(ScreenForegroundService),Exported = false, ForegroundServiceType = ForegroundService.TypeNone)]
public class ScreenForegroundService : Service
{
    
    private const string ChannelId = "screen_service_channel";
    private const int NotificationId = 1001;
    private const string ChannelName = "Screen State Monitoring";
    
    private BroadcastReceiver? _screenEventsReceiver;
    
    private static void Execute(Context context, string action)
    {
        var intent = new Intent(context, typeof(ScreenForegroundService));
        intent.SetAction(action);
        context.StartForegroundService(intent);
    }
    
    public static void Start(Context context) => Execute(context, "START");
    public static void Stop(Context context) => Execute(context, "STOP");

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        _screenEventsReceiver = new ScreenEventsReceiver();
        var filter = new IntentFilter();
        filter.AddAction(Intent.ActionScreenOn);
        filter.AddAction(Intent.ActionScreenOff);
        RegisterReceiver(_screenEventsReceiver, filter);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP")
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
        }
        else
        {
            StartForeground(NotificationId, BuildNotification());
        }

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (_screenEventsReceiver is not null)
        {
            UnregisterReceiver(_screenEventsReceiver);
        }
        base.OnDestroy();
    }

    private Notification BuildNotification()
    {
        var manager = GetSystemService(Context.NotificationService) as NotificationManager;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Min
            );
            manager?.CreateNotificationChannel(channel);
        }

        var builder = new Notification.Builder(this, ChannelId)
            .SetContentTitle("Monitoring screen state")
            .SetSmallIcon(Resource.Mipmap.appicon_foreground)
            .SetOngoing(true)
            .SetAutoCancel(false);

        return builder.Build();
    }
}