using System.Web;
using Android.App;
using Android.Service.Dreams;
using BookHeaven.Reader.Features.Preferences.Services;

namespace BookHeaven.Reader.Services;

[Service(Name = "bookheaven.ScreenSaverService", Exported = true, Label = "BookHeaven", Permission = "android.permission.BIND_DREAM_SERVICE")]
[IntentFilter(["android.service.dreams.DreamService"])]
public sealed class ScreenSaverService : DreamService
{
    public override void OnDreamingStarted()
    {
        base.OnDreamingStarted();
        
        var appStateService = IPlatformApplication.Current!.Services.GetService<PreferencesService>();
        var coverPath = appStateService?.CurrentScreenSaverCoverPath;

        // C#
        var webView = new Android.Webkit.WebView(this);
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.AllowFileAccess = true;
        
        webView.LoadUrl("file:///android_asset/wwwroot/screen-saver.html" + (!string.IsNullOrEmpty(coverPath) ? "?cover=" + HttpUtility.UrlEncode(coverPath) : ""));

        SetContentView(webView);
    }

    public override void OnDreamingStopped()
    {
        base.OnDreamingStopped();
        // Aquí termina la lógica de tu screen saver
    }
}