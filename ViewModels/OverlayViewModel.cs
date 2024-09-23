using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookHeaven.Reader.ViewModels
{
	public partial class OverlayViewModel : ObservableObject
	{
		[ObservableProperty]
		private bool showFontSettings = false;
		[ObservableProperty]
		private bool showPageSettings = false;
	}
}
