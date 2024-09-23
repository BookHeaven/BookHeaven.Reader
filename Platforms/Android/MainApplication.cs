using Android.App;
using Android.OS;
using Android.Runtime;

namespace BookHeaven.Reader
{
	[Application]
	public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
	{

		protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	}
}
