using CommunityToolkit.Mvvm.ComponentModel;

namespace BookHeaven.Reader.ViewModels
{
	public partial class ReaderViewModel : ObservableObject
	{
		[ObservableProperty]
		private int currentChapter = -1;
		[ObservableProperty]
		private int? currentPage;
		[ObservableProperty]
		private int? totalPages;
		[ObservableProperty]
		private int? totalPagesPrev, totalPagesNext;
		[ObservableProperty]
		private int? wordsPerPage;

		[ObservableProperty]
		private bool showOverlay;
		[ObservableProperty]
		private bool showToc;

		partial void OnShowTocChanged(bool value)
		{
			if(value) ShowOverlay = false;
		}
	}
}