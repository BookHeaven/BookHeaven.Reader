using Android.App;
using Android.Content.PM;
using Android.Views;

namespace BookHeaven.Reader
{
	[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density, ScreenOrientation = ScreenOrientation.Portrait, Exported = true)]
	[IntentFilter([Android.Content.Intent.ActionMain], Categories = [Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryHome, Android.Content.Intent.CategoryLauncher])]
	public class MainActivity : MauiAppCompatActivity
	{

	}
}
