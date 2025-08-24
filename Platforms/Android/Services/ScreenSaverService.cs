using System.Web;
using Android.App;
using Android.Service.Dreams;

namespace BookHeaven.Reader.Services;

[Service(Name = "bookheaven.ScreenSaverService", Exported = true, Label = "BookHeaven", Permission = "android.permission.BIND_DREAM_SERVICE")]
[IntentFilter(["android.service.dreams.DreamService"])]
public class ScreenSaverService : DreamService
{
    public override void OnDreamingStarted()
    {
        base.OnDreamingStarted();
        
        var appStateService = IPlatformApplication.Current!.Services.GetService<AppStateService>();
        var coverBase64 = appStateService?.CurrentScreenSaverCoverPath;

        // C#
        var webView = new Android.Webkit.WebView(this);
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.AllowFileAccess = true;
        
        webView.LoadUrl("file:///android_asset/wwwroot/screen-saver.html" + (!string.IsNullOrEmpty(coverBase64) ? "?cover=" + HttpUtility.UrlEncode(coverBase64) : ""));

        SetContentView(webView);
    }

    public override void OnDreamingStopped()
    {
        base.OnDreamingStopped();
        // Aquí termina la lógica de tu screen saver
    }
}