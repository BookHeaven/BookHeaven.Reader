using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Abstractions.Services;
using BookHeaven.Core.Enums;
using BookHeaven.Core.Entities;
using BookHeaven.Core.DOM.Text.Enums;
using BookHeaven.Core.Shared;
using BookHeaven.Core.Features.Books;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Features.Reader.Models;
using BookHeaven.Reader.Components.Pages.Reader.Partials;
using BookHeaven.Reader.Features.Lifecycle.Services;
using BookHeaven.Reader.Features.Reader.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader;

public partial class Reader : IAsyncDisposable
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private PreferencesService PreferencesService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private OverlayService OverlayService { get; set; } = null!;
    [Inject] private IReaderSettingsService ReaderSettingsService { get; set; } = null!;
    [Inject] private IUrlBuilder UrlBuilder { get; set; } = null!;
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private IJSObjectReference? _module;
    private ReaderState _readerState = ReaderState.Uninitialized;
    private bool _recountPagesOnNextRender;
    private TextSelectionHandler? _textSelection;
    private BookAnnotation? _annotationToNavigate;
    private Guid _assetLoadToken = Guid.NewGuid();


    protected override async Task OnInitializedAsync()
    {
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderSettingsService.OnSettingChanged += OnSettingChanged;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Destroyed += OnDestroy;
        
        PreferencesService.CurrentScreenSaverCoverPath = UrlBuilder.CoverUrl(Id);
        await ReaderService.InitializeAsync(Id, PreferencesService.ProfileId);
        _readerState = ReaderState.Initialized;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            
#if ANDROID
            SetFullscreen(true);
#endif
            
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Reader.razor.js");
        }
        
        if(_module is null) return;

        if (_annotationToNavigate is not null && _readerState == ReaderState.Ready && _textSelection is not null)
        {
            var annotation = _annotationToNavigate;
            var targetPage = await _textSelection.GetPageForTextOffsetAsync(annotation.StartOffset);
            _annotationToNavigate = null;
            if (targetPage is null)
            {
                await AlertService.ShowToastAsync(
                    Translations.ANNOTATION_LOCATION_NOT_FOUND,
                    AlertSeverity.Warning);
            }
            else
            {
                ReaderService.NavigateToPage(targetPage.Value);
            }
        }

        switch (_readerState)
        {
            case ReaderState.Initialized:
                var readerContext = await GetReaderContextAsync();
                await ReaderService.LoadCachedPagesAsync(readerContext);
                if (ReaderService.State?.PagesPerChapter.Length > 0)
                {
                    ReaderService.NavigateToInitialPage();
                }
                else
                {
                    await UpdateTotalPages();
                }
                _assetLoadToken = Guid.NewGuid();
                _readerState = ReaderState.WaitingForAssets;
                StateHasChanged();
                break;
            case ReaderState.WaitingForAssets:
                var assetLoadToken = _assetLoadToken;
                await _module!.InvokeVoidAsync("WaitForAssetsToLoad");
                if (assetLoadToken != _assetLoadToken || _readerState != ReaderState.WaitingForAssets)
                {
                    break;
                }
                if (_textSelection is not null)
                {
                    await _textSelection.RestoreHighlightsAsync();
                }
                _readerState = ReaderState.Ready;
                RefreshUi();
                break;
        }

        if (_recountPagesOnNextRender)
        {
            _recountPagesOnNextRender = false;
            _ = UpdateTotalPages();
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ReaderService.SaveProgressAsync();
        PreferencesService.CurrentScreenSaverCoverPath = null;

        ReaderService.OnPageChanged -= RefreshUi;
        ReaderService.OnChapterChanged -= OnChapterChanged;
        ReaderService.OnTotalPagesChanged -= RefreshUi;
        ReaderSettingsService.OnSettingChanged -= OnSettingChanged;
        LifeCycleService.Paused -= OnPaused;
        LifeCycleService.Resumed -= OnResumed;
        LifeCycleService.Destroyed -= OnDestroy;

        ReaderService.Dispose();
        if (_module is not null)
        {
            try
            {
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
        _recountPagesOnNextRender = true;
        RefreshUi();
    }

    private void OnChapterChanged()
    {
        _assetLoadToken = Guid.NewGuid();
        _readerState = ReaderState.WaitingForAssets;
        RefreshUi();
    }

    private async Task UpdateTotalPages()
    {
        if(_module is null) return;
        var context = await GetReaderContextAsync();
        await ReaderService.CalculatePagesAsync(context);
        if (!ReaderService.IsReady)
        {
            ReaderService.NavigateToInitialPage();
        }
    }
    
    private async Task<ReaderContext> GetReaderContextAsync()
    {
        if(_module is null) throw new InvalidOperationException("JS module is not initialized.");
        var info = await _module.InvokeAsync<ReaderContextInfo>("GetReaderContext");
        var mode = info.Engine == "gecko" ? LineBreakingMode.Firefox : LineBreakingMode.Chrome;
        return new ReaderContext
        {
            ViewportWidth = info.ViewportWidth,
            ViewportHeight = info.ViewportHeight,
            PageWidth = (int)info.PageWidth,
            PageHeight = (int)info.PageHeight,
            LineBreakingMode = mode,
            ColumnCount = info.ColumnCount
        };
    }

    private Task NavigateToAnnotationAsync(BookAnnotation annotation)
    {
        if (!ReaderService.NavigateToAnnotation(annotation))
        {
            return AlertService.ShowToastAsync(
                Translations.ANNOTATION_LOCATION_NOT_FOUND,
                AlertSeverity.Warning);
        }

        _annotationToNavigate = annotation;
        OverlayService.ToggleOverlay();
        return Task.CompletedTask;
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
        try
        {
            await ReaderService.SaveProgressAsync();
        }
        catch
        {
            // Ignore errors during save on destroy
        }
    }

    private void RefreshUi() => InvokeAsync(StateHasChanged);

#if ANDROID
    private static void SetFullscreen(bool enabled)
    {
        var window = Platform.CurrentActivity?.Window;
        if (window is null) return;
        if (enabled) window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
        else window.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
    }
#endif

    private record ReaderContextInfo(int ViewportWidth, int ViewportHeight, double PageWidth, double PageHeight, string Engine, int ColumnCount);

    private enum ReaderState
    {
        Uninitialized,
        Initialized,
        WaitingForAssets,
        Ready
    }
}
