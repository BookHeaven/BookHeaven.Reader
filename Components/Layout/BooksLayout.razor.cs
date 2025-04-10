using BookHeaven.Domain.Features.ProfileSettingss;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Layout;

public partial class BooksLayout
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;

    private async Task<bool> CheckConnection()
    {
        if (!await ServerService.CanConnect())
        {
            await Toast.Make(Translations.CONNECTION_FAILED, ToastDuration.Long).Show();
            return false;
        }

        return true;
    }

    private async Task SyncProgress()
    {
        if(!await CheckConnection())
            return;

        await ServerService.UpdateProgressByProfile(AppStateService.ProfileId);
        await Toast.Make(Translations.SYNC_COMPLETED).Show();
    }

    private async Task BackupProfile()
    {
        if(!await CheckConnection())
            return;
        
        var getSettings = await Sender.Send(new GetProfileSettings.Query(AppStateService.ProfileId));
        if(getSettings.IsFailure) return;
        
        await ServerService.UpdateProfileSettings(getSettings.Value);
        await Toast.Make("Backup completed").Show();
    }
}