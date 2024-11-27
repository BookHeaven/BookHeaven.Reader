using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using BookHeaven.Reader.Services;
using Application = Microsoft.Maui.Controls.Application;

namespace BookHeaven.Reader
{
	public partial class App : Application
	{
		private readonly LifeCycleService _lifeCycleService;

		public App(LifeCycleService lifeCycleService)
		{
			InitializeComponent();
			_lifeCycleService = lifeCycleService;

			Current!.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			var window = new Window(new MainPage() { Title = "BookHeaven"});
#if WINDOWS
			window.Width = 562;
			window.Height = 750;
#endif
			window.Activated += (sender, args) => _lifeCycleService.OnResume();
			window.Deactivated += (sender, args) => _lifeCycleService.OnPause();
			window.Stopped += (sender, args) => _lifeCycleService.OnStop();
			window.Destroying += (sender, args) => _lifeCycleService.OnDestroy();

			return window;
		}

	}
}
