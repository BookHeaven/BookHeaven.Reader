using Microsoft.AspNetCore.Components;
using BookHeaven.Reader.Services;
using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Resources.Localization;
using CommunityToolkit.Maui.Alerts;

namespace BookHeaven.Reader.Components.Pages.Remote.Partials;

public partial class RemoteBook
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Parameter] public IServerService ServerService { get; set; } = null!;
    [Parameter] public Book Book { get; set; } = null!;
    
    [Parameter] public EventCallback OnBookDownloaded { get; set; }
    
    private string _serverUrl = null!;
    private bool Downloading { get; set; }

    protected override void OnInitialized()
    {
        _serverUrl = AppStateService.ServerUrl!;
    }

    private async Task Download(Book book)
    {
        Downloading = true;
        StateHasChanged();
        await ServerService.Download(book, AppStateService.ProfileId);
        Downloading = false;
        StateHasChanged();
        await Toast.Make(Translations.DOWNLOAD_SUCCESS).Show();
        await OnBookDownloaded.InvokeAsync();
    }
}