using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.AspNetCore.Components;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;

namespace BookHeaven.Reader.Components.Layout;

public partial class BooksLayout
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;

    private async Task SyncProgress()
    {
        if (!await ServerService.CanConnect())
        {
            await Toast.Make(Translations.CONNECTION_FAILED, ToastDuration.Long).Show();
            return;
        }

        await ServerService.UpdateProgressByProfile(AppStateService.ProfileId);
        await Toast.Make(Translations.SYNC_COMPLETED).Show();
    }
}