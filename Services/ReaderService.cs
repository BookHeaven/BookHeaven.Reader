using System.ComponentModel;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Features.ProfileSettingss;
using BookHeaven.Reader.Enums;
using MediatR;

namespace BookHeaven.Reader.Services;

public class ReaderService(
    AppStateService appStateService,
    ISender sender)
{
    public ProfileSettings ProfileSettings { get; set; } = null!;
    
    public Action<NavigationButton>? OnNavigationButtonClicked { get; set; }

    public async Task Initialize()
    {
        var getSettings = await sender.Send(new GetProfileSettings.Query(appStateService.ProfileId));
        ProfileSettings = getSettings.IsSuccess ? getSettings.Value : new() {ProfileId = appStateService.ProfileId};
        if (ProfileSettings.ProfileSettingsId == Guid.Empty)
        {
            await sender.Send(new AddProfileSettings.Command(ProfileSettings));
        }
        
        ProfileSettings.PropertyChanged += OnProfileSettingsChanged;
    }
    

    private async void OnProfileSettingsChanged(object? s, PropertyChangedEventArgs e)
    {
        await sender.Send(new UpdateProfileSettings.Command(ProfileSettings));
    }
}