using Android.App;
using Android.Content;
using Android.Content.PM;

namespace BookHeaven.Reader;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density, ScreenOrientation = ScreenOrientation.Unspecified, Exported = true)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryDefault, Intent.CategoryHome, Intent.CategoryLauncher])]
public class MainActivity : MauiAppCompatActivity
{

}