using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;

namespace BookHeaven.Reader
{
	public partial class MainPage : ContentPage
	{
		public MainPage()
		{
			InitializeComponent();
#if ANDROID
			var statusBehavior = new StatusBarBehavior()
			{
				StatusBarColor = Colors.White,
				StatusBarStyle = StatusBarStyle.DarkContent
			};
			Behaviors.Add(statusBehavior);
#endif
		}
	}
}
