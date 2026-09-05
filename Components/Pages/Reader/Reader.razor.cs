using BookHeaven.Core.Abstractions.Services;
using BookHeaven.Core.Features.Books;
using BookHeaven.Core.Features.Reader.Abstractions;
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
    private bool _refreshTotalPages;
    private bool _isSuspended;

    protected override async Task OnInitializedAsync()
    {
        ReaderService.OnPageChanged += RefreshUi;
        ReaderService.OnChapterChanged += RefreshUi;
        ReaderService.OnTotalPagesChanged += RefreshUi;
        ReaderSettingsService.OnSettingsChanged += OnSettingsChanged;
        
        var getBook = await Sender.Send(new GetBook.Query {BookId = Id});
        if (getBook.IsFailure)
        {
            await Toast.Make(getBook.Error.Description).Show();
            return;
        }
        AppStateService.CurrentScreenSaverCoverPath = UrlBuilder.CoverUrl(Id);
		
        await ReaderService.InitializeAsync(Id, AppStateService.ProfileId);
        if (!ReaderService.IsReady)
        {
            _refreshTotalPages = true;
        }
    }
    
    private void OnSettingsChanged()
    {
        _refreshTotalPages = true;
        InvokeAsync(StateHasChanged);
    }

    private void RefreshUi()
    {
        InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
#if ANDROID
                var activity = Platform.CurrentActivity!;
                activity.Window?.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
#endif
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Reader.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("Initialize", _dotNetReference);
        }
        if(_module is not null && ReaderService.IsReady && !_isFullyRendered)
        {
            await _module.InvokeVoidAsync("WaitForAssetsToLoad");
            _isFullyRendered = true;
            StateHasChanged();
        }
        if (_refreshTotalPages)
        {
            await UpdateTotalPages();
        }
    }
    
    private void OnPaused()
    {
        if(_isSuspended) return;
        _isSuspended = true;
        ReaderService.PauseTimer();
        
    }

    private void OnResumed()
    {
        if (!_isSuspended) return;
        _isSuspended = false;
        ReaderService.ResumeTimer();
    }
    
    private async void OnDestroy()
    {
        await ReaderService.SaveProgressAsync();
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
        if(_module is null) return;
        _refreshTotalPages = false;
        var pagesArray = await _module.InvokeAsync<int[]>("GetPageCount");
        ReaderService.SetTotalPages(pagesArray);

        if (!ReaderService.IsReady)
        {
            ReaderService.NavigateToInitialPage();
            await InvokeAsync(StateHasChanged);
        }
    }
    
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ReaderService.SaveProgressAsync();
        AppStateService.CurrentScreenSaverCoverPath = null;
        ReaderService.OnPageChanged -= RefreshUi;
        ReaderService.OnChapterChanged -= RefreshUi;
        ReaderService.OnTotalPagesChanged -= RefreshUi;
        LifeCycleService.Resumed -= OnResumed;
        LifeCycleService.Paused -= OnPaused;
        LifeCycleService.Destroyed -= OnDestroy;
        ReaderService.Dispose();
        _dotNetReference.Dispose();
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("Dispose");
                await _module.DisposeAsync();
            }
        }
        catch {}
#if ANDROID
        var activity = Platform.CurrentActivity!;
        activity.Window?.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
#endif
        GC.SuppressFinalize(this);
    }
}