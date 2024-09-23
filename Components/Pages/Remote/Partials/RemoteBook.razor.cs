using Microsoft.AspNetCore.Components;
using BookHeaven.Reader.Services;
using BookHeaven.Domain.Entities;

namespace BookHeaven.Reader.Components.Pages.Remote.Partials;

public partial class RemoteBook
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Parameter] public IServerService ServerService { get; set; } = null!;
    [Parameter] public Book Book { get; set; } = null!;
    private string _serverUrl = null!;
    private bool Downloading { get; set; }

    protected override void OnInitialized()
    {
        _serverUrl = AppStateService.ServerUrl!;
    }

    private async Task Download(Book book)
    {
        Downloading = true;
        await ServerService.Download(book, AppStateService.ProfileId);
        Downloading = false;
    }
}