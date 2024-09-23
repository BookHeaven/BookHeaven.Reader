using System.ComponentModel;
using System.Text.Json;
using EpubManager;
using EpubManager.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BookHeaven.Reader.Services;
using BookHeaven.Reader.ViewModels;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Components.Pages.Reader;

public partial class Reader : IAsyncDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IDatabaseService DatabaseService { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IEpubReader EpubReader { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    
    private readonly ReaderViewModel _readerViewModel = new();

    private DateTime _entryTime;
    private DateTime _exitTime;
    private bool _isSuspended;

    private IJSObjectReference _module = null!;
    private ProfileSettings? _profileSettings;
    private DateTime _suspendStartTime;
    private TimeSpan _totalSuspendedTime;

    private Book? _book;
    private bool _bookLoading = true;
    private BookProgress _bookProgress = null!;
    private EpubBook? _epubBook;
    private EpubChapter? _epubChapter, _epubChapterPrev, _epubChapterNext;
    private bool _refreshTotalPages;
    private IReadOnlyList<string>? _styles;

    private int? _totalPagesPrev, _totalPagesNext;
    private int _totalWords;
    

    

    private decimal Progress => _epubBook != null && _epubChapter != null && _totalWords != 0
        ? (_epubBook.Content.GetWordCount(_readerViewModel.CurrentChapter) +
           _epubChapter.GetWordsPerPage((_readerViewModel.TotalPages ?? 0) + 1) *
           (_readerViewModel.CurrentPage + 1 ?? 0)) / (decimal)_totalWords * 100
        : 0;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _readerViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        LifeCycleService.Resumed -= OnResumed;
        LifeCycleService.Paused -= OnPaused;
        LifeCycleService.Destroyed -= OnDestroy;
        await SaveState();
        await _module.DisposeAsync();
#if ANDROID
            var activity = Platform.CurrentActivity!;
            var flags = Android.Views.WindowManagerFlags.Fullscreen;
            activity.Window?.ClearFlags(flags);
#endif
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
            var activity = Platform.CurrentActivity!;
            var flags = Android.Views.WindowManagerFlags.Fullscreen;
            activity.Window?.AddFlags(flags);
#endif

        var bookTask = DatabaseService.Get<Book>(Id);
        var profileSettingsTask =
            DatabaseService.GetBy<ProfileSettings>(x => x.ProfileId == AppStateService.ProfileId);
        var bookProgressTask = DatabaseService.GetBy<BookProgress>(x =>
            x.BookId == Id && x.ProfileId == AppStateService.ProfileId);

        await Task.WhenAll(bookTask, profileSettingsTask, bookProgressTask);

        _book = await bookTask;
        _profileSettings = await profileSettingsTask ?? new ProfileSettings
            { ProfileId = AppStateService.ProfileId };
        _bookProgress = (await bookProgressTask)!;

        _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import",
            "./Components/Pages/Reader/Reader.razor.js");
        await _module.InvokeVoidAsync("SetDotNetReference", DotNetObjectReference.Create(this));

        _readerViewModel.PropertyChanged += ViewModel_PropertyChanged;

        _entryTime = DateTime.Now;

        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Destroyed += OnDestroy;


        await LoadFromCache();
        if (_epubChapter == null)
            await LoadEpubBook();
        else
            _ = LoadEpubBook();
        _bookLoading = false;
        if (_bookProgress.ElapsedTime != TimeSpan.Zero)
        {
            if(_bookProgress.BookWordCount != 0)
            {
                _totalWords = _bookProgress.BookWordCount;
            }
            _readerViewModel.TotalPages = _bookProgress.PageCount;
            _totalPagesPrev = _bookProgress.PageCountPrev;
            _totalPagesNext = _bookProgress.PageCountNext;
            NavigateToChapter(_bookProgress.Page, _bookProgress.Chapter, false, false);
            _refreshTotalPages = true;
        }
        else
        {
            _bookProgress.StartDate = DateTimeOffset.Now;
            NavigateToChapter(0, 0);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_bookLoading && _refreshTotalPages) await UpdateTotalPages();
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        if (_isSuspended)
        {
            var resumeTime = DateTime.Now;
            _totalSuspendedTime += resumeTime - _suspendStartTime;
            _isSuspended = false;
        }
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        _isSuspended = true;
        _suspendStartTime = DateTime.Now;
    }

    private async void OnDestroy(object? sender, EventArgs e)
    {
        await SaveState();
    }

    private async Task LoadEpubBook()
    {
        _epubBook = await EpubReader.ReadAsync(Path.Combine(MauiProgram.BooksPath, $"{Id}.epub"), false);
        if (_totalWords == 0) _totalWords = _epubBook.Content.GetWordCount();
        if (_styles == null)
        {
            _styles = _epubBook.Content.Styles;
            _ = WriteToCache("styles", _styles);
        }

        StateHasChanged();
    }

    private async Task LoadFromCache()
    {
        _styles = await LoadFromCache<IReadOnlyList<string>>("styles");
        var chapters = await LoadFromCache<List<EpubChapter?>>("progress");
        if (chapters != null)
        {
            _epubChapter = chapters[0]!;
            _epubChapterPrev = chapters[1];
            _epubChapterNext = chapters[2];
        }
    }

    private async Task UpdateChapterCache()
    {
        if (_epubChapter == null) return;
        List<EpubChapter?> chapters = [_epubChapter, _epubChapterPrev, _epubChapterNext];
        await WriteToCache("progress", chapters);
    }

    private async Task<T?> LoadFromCache<T>(string name)
    {
        try
        {
            var json = await File.ReadAllTextAsync(Path.Combine(MauiProgram.CachePath, $"{Id}-{name}.cache"));
            return JsonSerializer.Deserialize<T>(json)!;
        }
        catch (FileNotFoundException)
        {
            return default;
        }
    }

    private async Task WriteToCache<T>(string name, T item)
    {
        var json = JsonSerializer.Serialize(item);
        await File.WriteAllTextAsync(Path.Combine(MauiProgram.CachePath, $"{Id}-{name}.cache"), json);
    }


    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReaderViewModel.CurrentChapter):
                if (_epubBook == null || _readerViewModel.CurrentChapter == -1) return;

                if (_epubChapter == null)
                {
                    _epubChapter = _epubBook!.Content.ReadingOrder[_readerViewModel.CurrentChapter];
                    _epubChapter.Content =
                        await EpubReader.GetChapterContentAsync(_epubBook.FilePath, _epubBook.RootFolder,
                            _epubChapter.Path!);
                }

                if (_epubChapterPrev == null)
                {
                    _epubChapterPrev = _readerViewModel.CurrentChapter > 0
                        ? _epubBook.Content.ReadingOrder[_readerViewModel.CurrentChapter - 1]
                        : null;
                    if (_epubChapterPrev != null)
                        _epubChapterPrev.Content = await EpubReader.GetChapterContentAsync(_epubBook.FilePath,
                            _epubBook.RootFolder, _epubChapterPrev.Path!);
                }

                if (_epubChapterNext == null)
                {
                    _epubChapterNext = _readerViewModel.CurrentChapter < _epubBook.Content.ReadingOrder.Count - 1
                        ? _epubBook.Content.ReadingOrder[_readerViewModel.CurrentChapter + 1]
                        : null;
                    if (_epubChapterNext != null)
                        _epubChapterNext.Content = await EpubReader.GetChapterContentAsync(_epubBook.FilePath,
                            _epubBook.RootFolder, _epubChapterNext.Path!);
                }

                _refreshTotalPages = true;

                StateHasChanged();
                _ = UpdateChapterCache();

                break;
            case nameof(ReaderViewModel.CurrentPage):
                if (_readerViewModel.CurrentPage == null || _readerViewModel.CurrentPage == -1) return;
                StateHasChanged();
                break;
        }
    }

    private void OnChapterSelected(string path)
    {
        NavigateToChapter(0, _epubBook!.Content.ReadingOrder.First(x => x.Value.Path == path).Key);
    }

    private void OnSettingsChanged()
    {
        _refreshTotalPages = true;
    }

    private void OnGoToChapter(NavigationButton button)
    {
        if (button == NavigationButton.Next)
        {
            if (_readerViewModel.CurrentChapter < _epubBook!.Content.ReadingOrder.Count - 1)
            {
                if (_totalPagesNext != null)
                {
                    _totalPagesPrev = _readerViewModel.TotalPages;
                    _readerViewModel.TotalPages = _totalPagesNext;
                    _totalPagesNext = null;
                    _epubChapterPrev = _epubChapter;
                    _epubChapter = _epubChapterNext;
                    _epubChapterNext = null;
                }

                _readerViewModel.CurrentPage = 0;
                _readerViewModel.CurrentChapter++;
            }
        }
        else if(button == NavigationButton.Previous)
        {
            if (_readerViewModel.CurrentChapter > 0)
            {
                if (_totalPagesPrev != null)
                {
                    _readerViewModel.CurrentPage = _totalPagesPrev;
                    _totalPagesNext = _readerViewModel.TotalPages;
                    _readerViewModel.TotalPages = _totalPagesPrev;
                    _totalPagesPrev = null;
                    _epubChapterNext = _epubChapter;
                    _epubChapter = _epubChapterPrev;
                    _epubChapterPrev = null;
                }

                _readerViewModel.CurrentPage = 0;
                _readerViewModel.CurrentChapter--;
            }
        }

        _ = UpdateChapterCache();
    }

    private void NavigateToChapter(int page, int chapter, bool resetTotalPages = true, bool resetChapters = true)
    {
        if (resetTotalPages)
        {
            _readerViewModel.TotalPages = null;
            _totalPagesPrev = null;
            _totalPagesNext = null;
        }

        if (resetChapters)
        {
            _epubChapter = null;
            _epubChapterPrev = null;
            _epubChapterNext = null;
        }

        _readerViewModel.CurrentPage = null;
        _readerViewModel.CurrentChapter = -1;
        _readerViewModel.CurrentPage = page;
        _readerViewModel.CurrentChapter = chapter;

        _ = UpdateChapterCache();
    }

    private void NavigationButtonClicked(NavigationButton button)
    {
        if (button == NavigationButton.Overlay)
        {
            _readerViewModel.ShowOverlay = !_readerViewModel.ShowOverlay;
            return;
        }
        if (!CanGoDirection(button)) return;

        switch (button)
        {
            case NavigationButton.Next:
                NextPage();
                break;
            case NavigationButton.Previous:
                PreviousPage();
                break;
        }
    }

    private void NextPage()
    {
        if (_readerViewModel.CurrentPage < _readerViewModel.TotalPages)
        {
            _readerViewModel.CurrentPage++;
        }
        else
        {
            _readerViewModel.CurrentPage = null;
            if (_totalPagesNext != null)
            {
                _totalPagesPrev = _readerViewModel.TotalPages;
                _readerViewModel.TotalPages = _totalPagesNext;
                _totalPagesNext = null;
                _epubChapterPrev = _epubChapter;
                _epubChapter = _epubChapterNext;
                _epubChapterNext = null;
            }

            _readerViewModel.CurrentPage = 0;
            _readerViewModel.CurrentChapter++;
        }
    }

    private void PreviousPage()
    {
        if (_readerViewModel.CurrentPage > 0)
        {
            _readerViewModel.CurrentPage--;
        }
        else
        {
            _readerViewModel.CurrentPage = null;
            if (_totalPagesPrev != null)
            {
                _totalPagesNext = _readerViewModel.TotalPages;
                _readerViewModel.TotalPages = _totalPagesPrev;
                _totalPagesPrev = null;
                _epubChapterNext = _epubChapter;
                _epubChapter = _epubChapterPrev;
                _epubChapterPrev = null;
                _readerViewModel.CurrentPage = _readerViewModel.TotalPages;
            }
            else
            {
                _readerViewModel.CurrentPage = -1;
            }

            _readerViewModel.CurrentChapter--;
        }
    }

    private bool CanGoDirection(NavigationButton button)
    {
        return button switch
        {
            NavigationButton.Next => _readerViewModel.CurrentPage < _readerViewModel.TotalPages ||
                                        _readerViewModel.CurrentChapter < _epubBook!.Content.ReadingOrder.Count - 1,
            NavigationButton.Previous => _readerViewModel.CurrentPage > 0 || _readerViewModel.CurrentChapter > 0,
            _ => false
        };
    }

    [JSInvokable("OnKeyDown")]
    public void OnKeyDown(string key)
    {
        switch (key)
        {
            case "PageDown":
                NavigationButtonClicked(NavigationButton.Next);
                break;
            case "PageUp":
                NavigationButtonClicked(NavigationButton.Previous);
                break;
        }
    }

    private async Task UpdateTotalPages()
    {
        _refreshTotalPages = false;
        var pagesArray = await _module.InvokeAsync<int?[]>("GetPageCount");

        _totalPagesPrev = pagesArray[0] != null ? pagesArray[0] - 1 : null;
        _readerViewModel.TotalPages = pagesArray[1] - 1;
        _totalPagesNext = pagesArray[2] != null ? pagesArray[2] - 1 : null;
        if (_readerViewModel.CurrentPage > _readerViewModel.TotalPages)
            _readerViewModel.CurrentPage = _readerViewModel.TotalPages;

        StateHasChanged();
    }


    private void UpdateProgress()
    {
        if (_readerViewModel.TotalPages == null) return;

        _bookProgress.Chapter = _readerViewModel.CurrentChapter;
        _bookProgress.Page = _readerViewModel.CurrentPage!.Value;
        _bookProgress.Progress = Progress;
        _bookProgress.BookWordCount = _totalWords;
        _bookProgress.PageCount = _readerViewModel.TotalPages!.Value;
        _bookProgress.PageCountPrev = _totalPagesPrev;
        _bookProgress.PageCountNext = _totalPagesNext;
    }

    private void SaveElapsedTime()
    {
        var elapsedTime = _exitTime - _entryTime - _totalSuspendedTime;
        _bookProgress.ElapsedTime += elapsedTime;
        _bookProgress.LastRead = DateTimeOffset.Now;
        if (_readerViewModel.CurrentChapter == _epubBook!.Content.ReadingOrder.Count - 1 &&
            _readerViewModel.CurrentPage == _readerViewModel.TotalPages) _bookProgress.EndDate = DateTimeOffset.Now;
    }

    private async Task SaveState()
    {
        _exitTime = DateTime.Now;
        if (_profileSettings != null) await DatabaseService.AddOrUpdate(_profileSettings!);
        UpdateProgress();
        SaveElapsedTime();
        await DatabaseService.AddOrUpdate(_bookProgress);
        await DatabaseService.SaveChanges();
    }
}