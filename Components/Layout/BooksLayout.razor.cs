using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.AspNetCore.Components;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;

namespace BookHeaven.Reader.Components.Layout;

public partial class BooksLayout
{
    private bool _buttonPressed;

    private bool _showDropdownMenu;
    
    [Inject] AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;

    private void ToggleDropdownMenu()
    {
        _buttonPressed = true;
        _showDropdownMenu = !_showDropdownMenu;
    }

    private void CloseDropdownMenu()
    {
        if (_buttonPressed) _showDropdownMenu = false;
        _buttonPressed = false;
    }

    private async Task SyncProgress()
    {
        if (!await ServerService.CanConnect(AppStateService.ServerUrl))
        {
            await Toast.Make(Translations.Connection_failed, ToastDuration.Long).Show();
            return;
        }

        await ServerService.UpdateProgressByProfile(AppStateService.ProfileId);
        await Toast.Make(Translations.SYNC_COMPLETED).Show();
    }
}