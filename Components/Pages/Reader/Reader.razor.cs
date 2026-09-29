using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Abstractions.Services;
using BookHeaven.Core.Features.Books;
using BookHeaven.Core.Features.Reader.Abstractions;
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
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private DotNetObjectReference<Reader> _dotNetReference = null!;
    private IJSObjectReference? _module;
    private bool _isFullyRendered;
    private bool _recountPagesOnNextRender;


    protected override void OnInitialized()
    {
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += OnChapterChanged;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderSettingsService.OnSettingChanged += OnSettingChanged;
        LifeCycleService.Paused += OnPaused;
        LifeCycleService.Resumed += OnResumed;
        LifeCycleService.Destroyed += OnDestroy;

        AppStateService.CurrentScreenSaverCoverPath = UrlBuilder.CoverUrl(Id);
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
            await ReaderService.InitializeAsync(Id, AppStateService.ProfileId);
            StateHasChanged();
            if (!ReaderService.IsReady)
            {
                _ = UpdateTotalPages();
            }
        }
        
        if (_module is not null && ReaderService.IsReady && !_isFullyRendered)
        {
            await _module.InvokeVoidAsync("WaitForAssetsToLoad");
            _isFullyRendered = true;
            RefreshUi();
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
        _recountPagesOnNextRender = true;
        RefreshUi();
    }

    private void OnChapterChanged()
    {
        _isFullyRendered = false;
        RefreshUi();
    }

    private async Task UpdateTotalPages()
    {
        if(_module is null) return;
        var size = await _module.InvokeAsync<int[]>("GetPageDimensions");
        await ReaderService.CalculatePagesAsync(size[0], size[1]);
        if (!ReaderService.IsReady)
        {
            ReaderService.NavigateToInitialPage();
        }
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
