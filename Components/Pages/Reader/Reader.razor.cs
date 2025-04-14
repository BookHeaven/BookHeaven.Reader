using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.BooksProgress;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Extensions;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui.Alerts;
using EpubManager;
using EpubManager.Entities;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Style = EpubManager.Entities.Style;

namespace BookHeaven.Reader.Components.Pages.Reader;

public partial class Reader : IAsyncDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IEpubReader EpubReader { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    [Inject] private ReaderService ReaderService { get; set; } = null!;

    private readonly Stopwatch _readingStopwatch = new();
    
    private bool _isSuspended;

    private DotNetObjectReference<Reader> _dotNetReference = null!;
    private IJSObjectReference _module = null!;

    private Book? _book;
    private bool _bookLoading = true;
    private BookProgress _bookProgress = null!;
    private EpubBook? _epubBook;
    private bool _refreshTotalPages;
    private IReadOnlyList<Style> _styles = [];

    private int _totalWords;

    //private SpineItem? _current, _previous, _next;
    private SpineItem? Current => _epubBook?.Content.Spine.ElementAtOrDefault(ReaderService.CurrentChapter);
	private SpineItem? Next => _epubBook?.Content.Spine.ElementAtOrDefault(ReaderService.CurrentChapter + 1);
	private SpineItem? Previous => _epubBook?.Content.Spine.ElementAtOrDefault(ReaderService.CurrentChapter - 1);
    private EpubChapter? CurrentChapter => _epubBook?.Content.GetChapterFromTableOfContents(Current?.Id);
    private string ChapterTitle => CurrentChapter?.Title ?? Current?.Title ?? string.Empty;
    
	private decimal Progress => _epubBook != null && Current != null && _totalWords != 0
        ? (_epubBook.Content.GetWordCount(ReaderService.CurrentChapter) +
           Current.GetWordsPerPage(ReaderService.TotalPages + 1) *
           (ReaderService.CurrentPage + 1)) / (decimal)_totalWords * 100
        : 0;

    protected override async Task OnInitializedAsync()
    {
        await ReaderService.Initialize();
        ReaderService.ProfileSettings.PropertyChanged += OnProfileSettingsChanged;
        ReaderService.OnPageChanged += StateHasChanged;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += StateHasChanged;
        ReaderService.OnChapterSelected += OnChapterSelected;
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
                await Toast.Make(getBook.Error.Description!).Show();
                return;
            }
            _book = getBook.Value;
            
            var getBookProgress = await bookProgressTask;
            if (getBookProgress.IsFailure)
            {
                await Toast.Make(getBookProgress.Error.Description!).Show();
                return;
            }

            _bookProgress = getBookProgress.Value;

            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Reader.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("SetDotNetReference", _dotNetReference);

            if (_bookProgress.EndDate is null)
            {
                _readingStopwatch.Start();

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
    
    private void OnPaused(object? sender, EventArgs e)
    {
        if(_isSuspended) return;
        
        _isSuspended = true;
        _readingStopwatch.Stop();
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        if (!_isSuspended) return;
        
        _readingStopwatch.Start();
        _isSuspended = false;
    }
    
    private async void OnDestroy(object? sender, EventArgs e)
    {
        await UpdateProgress();
    }

    private async Task LoadEpubBook()
    {
        _epubBook = await EpubReader.ReadAsync(_book!.EpubPath(MauiProgram.BooksPath), false);
        if (_totalWords == 0) _totalWords = _epubBook.Content.GetWordCount();
        if (_styles.Count == 0)
        {
            _styles = _epubBook.Content.Styles;
            //_ = WriteToCache(CacheKey.Styles, _styles);
        }
        ReaderService.TotalChapters = _epubBook.Content.Spine.Count;

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

    private async Task LoadFromCache()
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
                _next = chapters[2];*/
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

    private void OnProfileSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        _refreshTotalPages = true;
        StateHasChanged();
    }

    private async void OnChapterChanged()
    {
        var tasks = new List<Task>();
                
        if (Current is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Current.TextContent = await EpubReader.ApplyTextContentProcessing(Current.TextContent);
                Current.IsContentProcessed = true;
            }));
        }
        if (Previous is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Previous.TextContent = await EpubReader.ApplyTextContentProcessing(Previous.TextContent);
                Previous.IsContentProcessed = true;
            }));
        }
        if (Next is { IsContentProcessed: false })
        {
            tasks.Add(Task.Run(async () =>
            {
                Next.TextContent = await EpubReader.ApplyTextContentProcessing(Next.TextContent);
                Next.IsContentProcessed = true;
            }));
        }
        if (tasks.Count > 0) await Task.WhenAll(tasks);
        
        //_ = UpdateChapterCache();
        
        _refreshTotalPages = true;
        StateHasChanged();
    }

    private void OnChapterSelected(string itemId)
    {
        ReaderService.NavigateTo(0, _epubBook!.Content.Spine.FindIndex(x => x.Id == itemId));
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
        _readingStopwatch.Stop();
        
        _bookProgress.Chapter = ReaderService.CurrentChapter;
        _bookProgress.Page = ReaderService.CurrentPage;
        _bookProgress.BookWordCount = _totalWords;
        _bookProgress.PageCount = ReaderService.TotalPages;
        _bookProgress.PageCountPrev = ReaderService.TotalPagesPrev;
        _bookProgress.PageCountNext = ReaderService.TotalPagesNext;

        if (_bookProgress.EndDate is null)
        {
            _bookProgress.Progress = Progress;
            _bookProgress.ElapsedTime += _readingStopwatch.Elapsed;
            
            _bookProgress.LastRead = DateTimeOffset.Now;
            if (ReaderService.CurrentChapter == _epubBook!.Content.Spine.Count - 1 &&
                ReaderService.CurrentPage == ReaderService.TotalPages) _bookProgress.EndDate = DateTimeOffset.Now;
        }
        
        await Sender.Send(new UpdateBookProgress.Command(_bookProgress));
    }
    
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        EpubReader.Dispose();
        ReaderService.OnPageChanged -= StateHasChanged;
        ReaderService.OnChapterChanged -= OnChapterChanged;
        ReaderService.OnTotalPagesChanged -= StateHasChanged;
        ReaderService.OnChapterSelected -= OnChapterSelected;
        ReaderService.ProfileSettings.PropertyChanged -= OnProfileSettingsChanged;
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