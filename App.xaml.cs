using BookHeaven.Reader.Services;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BookHeaven.Reader;

public partial class App : Application
{
	private readonly LifeCycleService _lifeCycleService;
		
	private Window? _window;

	public App(LifeCycleService lifeCycleService)
	{
		InitializeComponent();
		_lifeCycleService = lifeCycleService;

		Current!.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (_window is null)
		{
			_window = new Window(new MainPage() { Title = "BookHeaven"});
#if WINDOWS
				_window.Width = 562;
				_window.Height = 750;
#endif
			_window.Activated += (sender, args) => _lifeCycleService.OnResume();
			_window.Deactivated += (sender, args) => _lifeCycleService.OnPause();
			_window.Stopped += (sender, args) => _lifeCycleService.OnStop();
			_window.Destroying += (sender, args) => _lifeCycleService.OnDestroy();
		}
			

		return _window;
	}

}