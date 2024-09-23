using CommunityToolkit.Mvvm.ComponentModel;
using EpubManager.Entities;

namespace BookHeaven.Reader.ViewModels
{
	public partial class ReaderViewModel : ObservableObject
	{
		[ObservableProperty]
		private int currentChapter = -1;
		[ObservableProperty]
		private int? currentPage = null;
		[ObservableProperty]
		private int? totalPages = null;
		[ObservableProperty]
		private int? totalPagesPrev, totalPagesNext = null;
		[ObservableProperty]
		private int? wordsPerPage = null;
		//[ObservableProperty]
		//private int wordsReadSoFar = -1;

		[ObservableProperty]
		private bool showOverlay = false;
		[ObservableProperty]
		private bool showSpine = false;
		//[ObservableProperty]
		//private bool showFontSettings = false;
		//[ObservableProperty]
		//private bool showPageSettings = false;

		//[ObservableProperty]
		//private double fontSize = 16;
		//[ObservableProperty]
		//private double lineHeight = 0;
		//[ObservableProperty]
		//private double letterSpacing = 0;
		//[ObservableProperty]
		//private double wordSpacing = 0.25;
		//[ObservableProperty]
		//private double paragraphSpacing = 10;
		//[ObservableProperty]
		//private double textIndent = 1;
		//[ObservableProperty]
		//private double horizontalMargin = 3;
		//[ObservableProperty]
		//private double verticalMargin = 1;

		partial void OnShowSpineChanged(bool value)
		{
			if(value) ShowOverlay = false;
		}
	}
}
