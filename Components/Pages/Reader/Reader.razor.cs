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
using BookHeaven.Reader.Extensions;
using CommunityToolkit.Maui.Alerts;
using Style = EpubManager.Entities.Style;
#if ANDROID
using Android.Views;
#endif

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

    private DotNetObjectReference<Reader> _dotNetReference = null!;
    private IJSObjectReference _module = null!;
    private ProfileSettings _profileSettings = new();
    private DateTime _suspendStartTime;
    private TimeSpan _totalSuspendedTime;

    private Book? _book;
    private bool _bookLoading = true;
    private BookProgress _bookProgress = null!;
    private EpubBook? _epubBook;
    private bool _refreshTotalPages;
    private IReadOnlyList<Style>? _styles;

    private int? _totalPagesPrev, _totalPagesNext;
    private int _totalWords;

    private SpineItem? _current, _previous, _next;
    private SpineItem? Current => _epubBook?.Content.Spine.ElementAtOrDefault(_readerViewModel.CurrentChapter) ?? _current;
	private SpineItem? Next => _epubBook?.Content.Spine.ElementAtOrDefault(_readerViewModel.CurrentChapter + 1) ?? _next;
	private SpineItem? Previous => _epubBook?.Content.Spine.ElementAtOrDefault(_readerViewModel.CurrentChapter - 1) ?? _previous;
    private EpubChapter? CurrentChapter => _epubBook?.Content.GetChapterFromTableOfContents(Current?.Id);
    private string ChapterTitle => CurrentChapter?.Title ?? Current?.Title ?? string.Empty;
    
	private decimal Progress => _epubBook != null && Current != null && _totalWords != 0
        ? (_epubBook.Content.GetWordCount(_readerViewModel.CurrentChapter) +
           Current.GetWordsPerPage((_readerViewModel.TotalPages ?? 0) + 1) *
           (_readerViewModel.CurrentPage + 1 ?? 0)) / (decimal)_totalWords * 100
        : 0;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _readerViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        LifeCycleService.Resumed -= OnResumed;
        LifeCycleService.Paused -= OnPaused;
        LifeCycleService.Destroyed -= OnDestroy;
        await SaveState();
        await _module.InvokeVoidAsync("Dispose");
        await _module.DisposeAsync();
        _dotNetReference.Dispose();
#if ANDROID
        var activity = Platform.CurrentActivity!;
        activity.Window?.ClearFlags(WindowManagerFlags.Fullscreen);
#endif
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
#if ANDROID
            var activity = Platform.CurrentActivity!;
            activity.Window?.AddFlags(WindowManagerFlags.Fullscreen);
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
        _dotNetReference = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("SetDotNetReference", _dotNetReference);

        _readerViewModel.PropertyChanged += ViewModel_PropertyChanged;

        _entryTime = DateTime.Now;

        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Destroyed += OnDestroy;


        await LoadFromCache();
        if (Current == null)
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
            NavigateToChapter(_bookProgress.Page, _bookProgress.Chapter, false);
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
        _epubBook = await EpubReader.ReadAsync(_book!.GetEpubPath(), false);
        if (_totalWords == 0) _totalWords = _epubBook.Content.GetWordCount();
        if (_styles == null)
        {
            _styles = _epubBook.Content.Styles;
            _ = WriteToCache(CacheKey.Styles, _styles);
        }

        if(_current != null && _current.Id == Current?.Id)
        {
            Current!.TextContent = _current.TextContent;
			Current.IsContentProcessed = _current.IsContentProcessed;
            _current = null;
		}

		if (_previous != null && _previous.Id == Previous?.Id)
		{
			Previous!.TextContent = _previous.TextContent;
			Previous.IsContentProcessed = _previous.IsContentProcessed;
            _previous = null;
		}

		if (_next != null && _next.Id == Next?.Id)
		{
			Next!.TextContent = _next.TextContent;
			Next.IsContentProcessed = _next.IsContentProcessed;
            _next = null;
		}
		StateHasChanged();
    }

    private async Task LoadFromCache()
    {
        try
        {
            _styles = await LoadFromCache<IReadOnlyList<Style>>(CacheKey.Styles);
            var chapters = await LoadFromCache<List<SpineItem?>>(CacheKey.Progress);
            if (chapters != null)
            {
                _current = chapters[0]!;
                _previous = chapters[1];
                _next = chapters[2];
            }
            StateHasChanged();
        }
        catch (Exception)
        {
            await Toast.Make("Error loading cache").Show();
        }
        
    }

    private async Task UpdateChapterCache()
    {
        if (Current == null) return;
        List<SpineItem?> chapters = [Current, Previous, Next];
        await WriteToCache(CacheKey.Progress, chapters);
    }

    private async Task<T?> LoadFromCache<T>(CacheKey key)
    {
        try
        {
            var json = await File.ReadAllTextAsync(_book!.GetCachePath(key));
            return JsonSerializer.Deserialize<T>(json)!;
        }
        catch (FileNotFoundException)
        {
            return default;
        }
    }

    private async Task WriteToCache<T>(CacheKey key, T item)
    {
        var json = JsonSerializer.Serialize(item);
        await File.WriteAllTextAsync(_book!.GetCachePath(key), json);
    }


    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReaderViewModel.CurrentChapter):
                if (_epubBook == null || _readerViewModel.CurrentChapter == -1) return;
                
                if (Current is { IsContentProcessed: false })
                {
                    Current.TextContent = await EpubReader.ApplyTextContentProcessing(Current.TextContent);
                    Current.IsContentProcessed = true;
                }
                if (Previous is { IsContentProcessed: false })
                {
                    Previous.TextContent = await EpubReader.ApplyTextContentProcessing(Previous.TextContent);
                    Previous.IsContentProcessed = true;
                }
                if (Next is { IsContentProcessed: false })
                {
                    Next.TextContent = await EpubReader.ApplyTextContentProcessing(Next.TextContent);
                    Next.IsContentProcessed = true;
                }

                _refreshTotalPages = true;

                StateHasChanged();
                _ = UpdateChapterCache();

                break;
            case nameof(ReaderViewModel.CurrentPage):
                if (_readerViewModel.CurrentPage is null or -1) return;
                StateHasChanged();
                break;
        }
    }

    private void OnChapterSelected(string itemId)
    {
        NavigateToChapter(0, _epubBook!.Content.Spine.FindIndex(x => x.Id == itemId));
    }

    private void OnSettingsChanged()
    {
        _refreshTotalPages = true;
    }

    private void OnGoToChapter(NavigationButton button)
    {
        switch (button)
        {
            case NavigationButton.Next:
            {
                if (_readerViewModel.CurrentChapter < _epubBook!.Content.Spine.Count - 1)
                {
                    if (_totalPagesNext != null)
                    {
                        _totalPagesPrev = _readerViewModel.TotalPages;
                        _readerViewModel.TotalPages = _totalPagesNext;
                        _totalPagesNext = null;
                    }

                    _readerViewModel.CurrentPage = 0;
                    _readerViewModel.CurrentChapter++;
                }

                break;
            }
            case NavigationButton.Previous:
            {
                if (_readerViewModel.CurrentChapter > 0)
                {
                    if (_totalPagesPrev != null)
                    {
                        _readerViewModel.CurrentPage = _totalPagesPrev;
                        _totalPagesNext = _readerViewModel.TotalPages;
                        _readerViewModel.TotalPages = _totalPagesPrev;
                        _totalPagesPrev = null;
                    }

                    _readerViewModel.CurrentPage = 0;
                    _readerViewModel.CurrentChapter--;
                }

                break;
            }
        }

        _ = UpdateChapterCache();
    }

    private void NavigateToChapter(int page, int chapter, bool resetTotalPages = true)
    {
        if (resetTotalPages)
        {
            _readerViewModel.TotalPages = null;
            _totalPagesPrev = null;
            _totalPagesNext = null;
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
                                        _readerViewModel.CurrentChapter < _epubBook!.Content.Spine.Count - 1,
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

        _totalPagesPrev = pagesArray[0] - 1;
        _readerViewModel.TotalPages = pagesArray[1] - 1;
        _totalPagesNext = pagesArray[2] - 1;
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
        if (_readerViewModel.CurrentChapter == _epubBook!.Content.Spine.Count - 1 &&
            _readerViewModel.CurrentPage == _readerViewModel.TotalPages) _bookProgress.EndDate = DateTimeOffset.Now;
    }

    private async Task SaveState()
    {
        _exitTime = DateTime.Now;
        await DatabaseService.AddOrUpdate(_profileSettings);
        UpdateProgress();
        SaveElapsedTime();
        await DatabaseService.AddOrUpdate(_bookProgress);
        await DatabaseService.SaveChanges();
    }
}