using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.BooksProgress;
using BookHeaven.EpubManager;
using BookHeaven.EpubManager.Abstractions;
using BookHeaven.EpubManager.Entities;
using BookHeaven.EpubManager.Enums;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui.Alerts;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader;

public partial class Reader : IAsyncDisposable
{
    [Parameter] public Guid Id { get; set; }
    
    [Inject] private EbookManagerProvider EbookManagerProvider { get; set; } = null!;
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    [Inject] private ReaderService ReaderService { get; set; } = null!;
    [Inject] private ProfileSettingsService ProfileSettingsService { get; set; } = null!;

    private IEbookReader EbookReader { get; set; } = null!;

    private DateTimeOffset _entryTime;
    private DateTimeOffset _suspendStartTime;
    private TimeSpan _totalSuspendedTime;
    
    private bool _isSuspended;

    private DotNetObjectReference<Reader> _dotNetReference = null!;
    private IJSObjectReference _module = null!;

    private Book? _book;
    private bool _bookLoading = true;
    private BookProgress _bookProgress = null!;
    private Ebook? _ebook;
    private bool _refreshTotalPages;
    private IReadOnlyList<Stylesheet> _styles = [];

    private int _totalWords;

    //private SpineItem? _current, _previous, _next;
    private Chapter? Current => _ebook?.Content.Chapters.ElementAtOrDefault(ReaderService.CurrentChapter);
	private Chapter? Next => _ebook?.Content.Chapters.ElementAtOrDefault(ReaderService.CurrentChapter + 1);
	private Chapter? Previous => _ebook?.Content.Chapters.ElementAtOrDefault(ReaderService.CurrentChapter - 1);
    private TocEntry? CurrentChapter => _ebook?.Content.GetChapterFromTableOfContents(Current?.Identifier);
    private string ChapterTitle => CurrentChapter?.Title ?? Current?.Title ?? string.Empty;
    
	private decimal Progress => _ebook != null && Current != null && _totalWords != 0
        ? (_ebook.Content.GetWordCount(ReaderService.CurrentChapter) +
           Current.WordsPerPage(ReaderService.TotalPages + 1) *
           (ReaderService.CurrentPage + 1)) / (decimal)_totalWords * 100
        : 0;

    protected override async Task OnInitializedAsync()
    {
        await ProfileSettingsService.LoadSettings();
        ProfileSettingsService.OnProfileSettingsChanged += OnProfileSettingsChanged;
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderService.OnChapterSelected += OnChapterSelected;
    }

    private void RefreshUi()
    {
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_bookLoading && _refreshTotalPages)
        {
            await UpdateTotalPages();
        }
        if (firstRender)
        {
    #if ANDROID
                var activity = Platform.CurrentActivity!;
                activity.Window?.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
    #endif

            var bookTask = Sender.Send(new GetBook.Query(Id));
            var bookProgressTask = Sender.Send(new GetBookProgressByProfile.Query(Id, AppStateService.ProfileId));

            await Task.WhenAll(bookTask, bookProgressTask);

            var getBook = await bookTask;
            if (getBook.IsFailure)
            {
                await Toast.Make(getBook.Error.Description).Show();
                return;
            }
            _book = getBook.Value;
            AppStateService.CurrentScreenSaverCoverPath = _book.CoverPath();
            
            var getBookProgress = await bookProgressTask;
            if (getBookProgress.IsFailure)
            {
                await Toast.Make(getBookProgress.Error.Description).Show();
                return;
            }

            _bookProgress = getBookProgress.Value;

            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Reader.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("SetDotNetReference", _dotNetReference);

            if (_bookProgress.EndDate is null)
            {
                _entryTime = DateTimeOffset.UtcNow;

                LifeCycleService.Resumed += OnResumed;
                LifeCycleService.Paused += OnPaused;
                LifeCycleService.Destroyed += OnDestroy;
            }

            /*await LoadFromCache();
            if (Current == null)
                await LoadEpubBook();
            else
                _ = LoadEpubBook();*/
            await LoadEpubBook();
            _bookLoading = false;
            if (_bookProgress.ElapsedTime != TimeSpan.Zero)
            {
                if(_bookProgress.BookWordCount != 0)
                {
                    _totalWords = _bookProgress.BookWordCount;
                }
                
                ReaderService.SetTotalPages(_bookProgress.PageCount, _bookProgress.PageCountPrev, _bookProgress.PageCountNext);
                ReaderService.NavigateTo(_bookProgress.Page, _bookProgress.Chapter);
                
            }
            else
            {
                _bookProgress.StartDate = DateTimeOffset.Now;
                ReaderService.NavigateTo(0,0);
            }
        }
    }
    
    private void OnPaused()
    {
        if(_isSuspended) return;
        
        _suspendStartTime = DateTimeOffset.UtcNow;
        _isSuspended = true;
        
    }

    private void OnResumed()
    {
        if (!_isSuspended) return;
        
        _totalSuspendedTime += DateTimeOffset.UtcNow - _suspendStartTime;
        _isSuspended = false;
    }
    
    private async void OnDestroy()
    {
        await UpdateProgress();
    }

    private async Task LoadEpubBook()
    {
        EbookReader = EbookManagerProvider.GetReader(Format.Epub) ?? throw new Exception("Reader not found for provided format");
        
        _ebook = await EbookReader.ReadAllAsync(_book!.EpubPath());
        if (_totalWords == 0) _totalWords = _ebook.Content.GetWordCount();
        if (_styles.Count == 0)
        {
            _styles = _ebook.Content.Stylesheets;
            //_ = WriteToCache(CacheKey.Styles, _styles);
        }
        ReaderService.TotalChapters = _ebook.Content.Chapters.Count;

        /*if(_current != null && _current.Id == Current?.Id)
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
		}*/
		StateHasChanged();
    }

    /*private async Task LoadFromCache()
    {
        try
        {
            var getStyles = LoadFromCache<IReadOnlyList<Style>>(CacheKey.Styles);
            var getChapters = LoadFromCache<List<SpineItem?>>(CacheKey.Progress);
            await Task.WhenAll(getStyles, getChapters);
            
            _styles = await getStyles ?? [];
            var chapters = await getChapters;
            if (chapters != null)
            {
                /*_current = chapters[0]!;
                _previous = chapters[1];
                _next = chapters[2];#1#
            }
            StateHasChanged();
        }
        catch (Exception)
        {
            await Toast.Make("Error loading cache").Show();
        }
        
    }*/

    /*private async Task UpdateChapterCache()
    {
        if (Current == null) return;
        List<SpineItem?> chapters = [Current, Previous, Next];
        await WriteToCache(CacheKey.Progress, chapters);
    }*/

    /*private async Task<T?> LoadFromCache<T>(CacheKey key)
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
    }*/

    /*private async Task WriteToCache<T>(CacheKey key, T item)
    {
        var json = JsonSerializer.Serialize(item);
        await File.WriteAllTextAsync(_book!.GetCachePath(key), json);
    }*/

    private void OnProfileSettingsChanged(string? propertyName)
    {
        _refreshTotalPages = true;
        InvokeAsync(StateHasChanged);
    }

    private async void OnChapterChanged()
    {
        var tasks = new List<Task>();
                
        if (Current is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Current.Content = await EbookReader.ApplyHtmlProcessingAsync(Current.Content);
                Current.IsContentProcessed = true;
            }));
        }
        if (Previous is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Previous.Content = await EbookReader.ApplyHtmlProcessingAsync(Previous.Content);
                Previous.IsContentProcessed = true;
            }));
        }
        if (Next is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Next.Content = await EbookReader.ApplyHtmlProcessingAsync(Next.Content);
                Next.IsContentProcessed = true;
            }));
        }
        if (tasks.Count > 0) await Task.WhenAll(tasks);
        
        //_ = UpdateChapterCache();
        
        _refreshTotalPages = true;
        await InvokeAsync(StateHasChanged);
    }

    private void OnChapterSelected(string itemId)
    {
        ReaderService.NavigateTo(0, _ebook!.Content.Chapters.Where(x => x.Identifier == itemId).Index().First().Index);
    }

    [JSInvokable("OnKeyDown")]
    public void OnKeyDown(string key)
    {
        switch (key)
        {
            case "PageDown":
                ReaderService.NextPage();
                break;
            case "PageUp":
                ReaderService.PreviousPage();
                break;
        }
    }

    private async Task UpdateTotalPages()
    {
        _refreshTotalPages = false;
        var pagesArray = await _module.InvokeAsync<int[]>("GetPageCount");
        ReaderService.SetTotalPages(pagesArray[1], pagesArray[0], pagesArray[2]);
    }


    private async Task UpdateProgress()
    {
        if (ReaderService.TotalPages == -1) return;
        
        _bookProgress.Chapter = ReaderService.CurrentChapter;
        _bookProgress.Page = ReaderService.CurrentPage;
        _bookProgress.BookWordCount = _totalWords;
        _bookProgress.PageCount = ReaderService.TotalPages;
        _bookProgress.PageCountPrev = ReaderService.TotalPagesPrev;
        _bookProgress.PageCountNext = ReaderService.TotalPagesNext;

        if (_bookProgress.EndDate is null)
        {
            _bookProgress.Progress = Progress;
            _bookProgress.ElapsedTime += DateTimeOffset.UtcNow - _entryTime - _totalSuspendedTime;
            _bookProgress.LastRead = DateTimeOffset.Now;
            
            if (ReaderService.CurrentChapter == _ebook!.Content.Chapters.Count - 1 &&
                ReaderService.CurrentPage == ReaderService.TotalPages) _bookProgress.EndDate = DateTimeOffset.Now;
        }
        
        await Sender.Send(new UpdateBookProgress.Command(_bookProgress));
    }
    
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        AppStateService.CurrentScreenSaverCoverPath = null;
        EbookReader.Dispose();
        ReaderService.OnPageChanged -= RefreshUi;
        ReaderService.OnChapterChanged -= OnChapterChanged;
        ReaderService.OnTotalPagesChanged -= RefreshUi;
        ReaderService.OnChapterSelected -= OnChapterSelected;
        ProfileSettingsService.OnProfileSettingsChanged -= OnProfileSettingsChanged;
        if (_bookProgress.EndDate is null)
        {
            LifeCycleService.Resumed -= OnResumed;
            LifeCycleService.Paused -= OnPaused;
            LifeCycleService.Destroyed -= OnDestroy;
        }
        await UpdateProgress();
        await _module.InvokeVoidAsync("Dispose");
        await _module.DisposeAsync();
        _dotNetReference.Dispose();
#if ANDROID
        var activity = Platform.CurrentActivity!;
        activity.Window?.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
#endif
        GC.SuppressFinalize(this);
    }
}