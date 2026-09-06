using BookHeaven.Core.Abstractions.Services;
using BookHeaven.Core.Features.Books;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Features.Reader.Models;
using CommunityToolkit.Maui.Alerts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader;

public partial class Reader : IAsyncDisposable
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private IReaderSettingsService ReaderSettingsService { get; set; } = null!;
    [Inject] private IUrlBuilder UrlBuilder { get; set; } = null!;

    private DotNetObjectReference<Reader> _dotNetReference = null!;
    private IJSObjectReference? _module;
    private bool _isFullyRendered;
    private bool _isCountingPages;
    private bool _recountRequested;
    private int _recountTotal;
    private bool _needsInitialNavigation;
    private readonly ChapterRecountQueue _recountQueue = new();

    private double CountingProgress => _isCountingPages && _recountTotal > 0 ? 1 - (double)_recountQueue.Count / _recountTotal : 1;
    

    protected override async Task OnInitializedAsync()
    {
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderSettingsService.OnSettingChanged += OnSettingChanged;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Destroyed += OnDestroy;

        var getBook = await Sender.Send(new GetBook.Query { BookId = Id });
        if (getBook.IsFailure)
        {
            await Toast.Make(getBook.Error.Description).Show();
            return;
        }

        AppStateService.CurrentScreenSaverCoverPath = UrlBuilder.CoverUrl(Id);

        await ReaderService.InitializeAsync(Id, AppStateService.ProfileId);
        if (ReaderService.State is { } state)
        {
            if (!ReaderService.IsReady || state.AnyChaptersPending) _recountRequested = true;
            // Without cache there are no counts: navigate to the initial page after counting
            if (!ReaderService.IsReady && state.BookPages == 0) _needsInitialNavigation = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
#if ANDROID
            SetFullscreen(true);
#endif
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Reader.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("Initialize", _dotNetReference);
            if (ReaderService.State?.Ebook?.Content.Chapters is { } chapters)
            {
                var contents = chapters.Select(c => c.Content).ToArray();
                var stylesheetIndices = chapters.Select(c => c.Stylesheets.ToArray()).ToArray();
                await _module.InvokeVoidAsync("SetChapterData", contents, stylesheetIndices);
            }
        }

        if (_module is not null && ReaderService.IsReady && !_isFullyRendered)
        {
            await _module.InvokeVoidAsync("WaitForAssetsToLoad");
            _isFullyRendered = true;
            StateHasChanged();
        }

        if (_recountRequested)
        {
            _ = RecountPagesAsync();
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ReaderService.SaveProgressAsync();
        AppStateService.CurrentScreenSaverCoverPath = null;

        ReaderService.OnPageChanged -= RefreshUi;
        ReaderService.OnChapterChanged -= OnChapterChanged;
        ReaderService.OnTotalPagesChanged -= RefreshUi;
        ReaderSettingsService.OnSettingChanged -= OnSettingChanged;
        LifeCycleService.Paused -= OnPaused;
        LifeCycleService.Resumed -= OnResumed;
        LifeCycleService.Destroyed -= OnDestroy;

        ReaderService.Dispose();
        _dotNetReference.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("Dispose");
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

#if ANDROID
        SetFullscreen(false);
#endif
        GC.SuppressFinalize(this);
    }
    

    private void OnSettingChanged(string? settingName)
    {
        if(!string.IsNullOrEmpty(settingName) && settingName != nameof(ReaderSettingsService.ReaderSettings.SelectedFont)) return;
        ReaderService.InvalidatePageCounts();
        if (ReaderService.State is { } state)
        {
            if (_isCountingPages)
            {
                var pending = state.GetPendingChapterIndices();
                _recountQueue.Enqueue(pending, front: true);
                _recountQueue.Prioritize(state.ChapterNumber);
                _recountTotal = Math.Max(_recountTotal, _recountQueue.Count);
                InvokeAsync(() => PrioritizeChapters([state.ChapterNumber, .. pending.Except([state.ChapterNumber])]));
            }
            else
            {
                _recountRequested = true;
            }
        }
        RefreshUi();
    }

    private void OnChapterChanged()
    {
        if (_isCountingPages &&
            ReaderService.State is { } state &&
            state.IsChapterPending(state.ChapterNumber))
        {
            _recountQueue.Prioritize(state.ChapterNumber);
            InvokeAsync(() => PrioritizeChapters(state.ChapterNumber));
        }
        RefreshUi();
    }

    [JSInvokable("OnChapterCounted")]
    public void OnChapterCounted(int chapter, int pageCount)
    {
        ReaderService.SetChapterPageCount(chapter, pageCount);
        if (_isCountingPages) _recountQueue.Dequeue(chapter);
        RefreshUi();
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

    private void OnPaused()
    {
        ReaderService.PauseTimer();
    }

    private void OnResumed()
    {
        ReaderService.ResumeTimer();
    }

    private async void OnDestroy()
    {
        await ReaderService.SaveProgressAsync();
    }
    
    private async Task RecountPagesAsync()
    {
        if (_module is not { } module || ReaderService.State is not { } state || _isCountingPages) return;
        _recountRequested = false;
        _isCountingPages = true;
        QueuePendingChapters(state);
        _recountTotal = _recountQueue.Count;
        RefreshUi();
        if (_recountTotal == 0)
        {
            _isCountingPages = false;
            return;
        }

        try
        {
            if (state.BookPages == 0)
            {
                // Count all chapters in one go: no prior data to calculate progress
                await module.InvokeVoidAsync("CountChapters", _recountQueue.ToArray());
            }
            else
            {
                // Incremental recount: the user can continue reading meanwhile
                while (_recountQueue.TryDequeueNext(out var chapter))
                {
                    // The JS loop may have already counted it (prioritized on demand)
                    if (!state.IsChapterPending(chapter)) continue;
                    // The user may have changed chapters: prioritize the one being read
                    _recountQueue.Prioritize(state.ChapterNumber);
                    await module.InvokeVoidAsync("CountChapters", [chapter]);
                    RefreshUi();
                }
            }
            
            if (state.AnyChaptersPending)
            {
                QueuePendingChapters(state);
                if (_recountQueue.Count > 0)
                {
                    _recountTotal = Math.Max(_recountTotal, _recountQueue.Count);
                    await module.InvokeVoidAsync("CountChapters", _recountQueue.ToArray());
                }
            }

            if (_needsInitialNavigation)
            {
                _needsInitialNavigation = false;
                ReaderService.NavigateToInitialPage();
                RefreshUi();
            }
        }
        finally
        {
            _isCountingPages = false;
            RefreshUi();
        }
    }
    
    private void QueuePendingChapters(ReaderState state)
    {
        _recountQueue.Clear();
        _recountQueue.Enqueue(state.GetPendingChapterIndices());
        _recountQueue.Prioritize(state.ChapterNumber);
    }
    
    private async Task PrioritizeChapters(params int[] chapters)
    {
        if (_module is null || chapters.Length == 0) return;
        try
        {
            await _module.InvokeVoidAsync("Prioritize", chapters);
        }
        catch { }
    }

    private void RefreshUi() => InvokeAsync(StateHasChanged);

#if ANDROID
    private void SetFullscreen(bool enabled)
    {
        var window = Platform.CurrentActivity!.Window;
        if (window is null) return;
        var fullscreen = Android.Views.WindowManagerFlags.Fullscreen;
        if (enabled) window.AddFlags(fullscreen);
        else window.ClearFlags(fullscreen);
    }
#endif
}
