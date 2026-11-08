using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Abstractions.Services;
using BookHeaven.Core.Enums;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Features.Reader.Models;
using BookHeaven.Reader.Components.Pages.Reader.Partials.TextSelection;
using BookHeaven.Reader.Features.Lifecycle.Services;
using BookHeaven.Reader.Features.Preferences.Services;
using BookHeaven.Reader.Features.Reader.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader;

public sealed partial class Reader : IAsyncDisposable, IReaderViewAdapter
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private PreferencesService PreferencesService { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private LifeCycleService LifeCycleService { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private OverlayService OverlayService { get; set; } = null!;
    [Inject] private IReaderSettingsService ReaderSettingsService { get; set; } = null!;
    [Inject] private IUrlBuilder UrlBuilder { get; set; } = null!;
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private IJSObjectReference? _module;
    private TextSelectionHandler? _textSelection;

    protected override async Task OnInitializedAsync()
    {
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderSettingsService.OnSettingChanged += OnSettingChanged;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Stopped += OnPaused;
        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Destroyed += OnDestroy;
        
        PreferencesService.CurrentScreenSaverCoverPath = UrlBuilder.CoverUrl(Id);
        await ReaderService.InitializeAsync(Id, PreferencesService.ProfileId, this);
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
        
        if (_module is null) return;

        if (await ReaderService.AdvanceViewAsync())
        {
            StateHasChanged();
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
        LifeCycleService.Stopped -= OnPaused;
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
    }


    private void OnSettingChanged(string? settingName)
    {
        if(!string.IsNullOrEmpty(settingName) && settingName != nameof(ReaderSettingsService.ReaderSettings.SelectedFont)) return;
        ReaderService.RequestPageRecount();
        RefreshUi();
    }

    private void OnChapterChanged()
    {
        RefreshUi();
    }

    async Task<ReaderContext> IReaderViewAdapter.GetReaderContextAsync()
    {
        if(_module is null) throw new InvalidOperationException("JS module is not initialized.");
        var info = await _module.InvokeAsync<ReaderContextInfo>("GetReaderContext");
        return ReaderContext.FromBrowserInfo(info);
    }

    async Task IReaderViewAdapter.WaitForAssetsToLoadAsync()
    {
        if (_module is null) throw new InvalidOperationException("JS module is not initialized.");
        await _module.InvokeVoidAsync("WaitForAssetsToLoad");
    }

    Task IReaderViewAdapter.RestoreHighlightsAsync() =>
        _textSelection?.RestoreHighlightsAsync() ?? Task.CompletedTask;

    private async Task NavigateToAnnotationAsync(BookAnnotation annotation)
    {
        if(_textSelection is null) return;
        if (!ReaderService.NavigateToAnnotation(annotation))
        {
            await AlertService.ShowToastAsync(
                Translations.ANNOTATION_LOCATION_NOT_FOUND,
                AlertSeverity.Warning);
            return;
        }
        OverlayService.ToggleOverlay();
        var targetPage = await _textSelection.GetPageForTextOffsetAsync(annotation.StartOffset);
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
}
