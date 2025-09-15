using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui.Alerts;
using Microsoft.AspNetCore.Components;

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
        var download = await ServerService.DownloadBook(book, AppStateService.ProfileId);
        Downloading = false;
        StateHasChanged();
        if (download.IsFailure)
        {
            await Toast.Make(download.Error.Description).Show();
            return;
        }
        await Toast.Make(Translations.DOWNLOAD_SUCCESS).Show();
        await OnBookDownloaded.InvokeAsync();
    }
}