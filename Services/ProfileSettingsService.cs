using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Features.ProfileSettingss;
using MediatR;

namespace BookHeaven.Reader.Services;

public partial class ProfileSettingsService : IDisposable
{
    private readonly AppStateService _appStateService;
    private readonly ISender _sender;

    

    public ProfileSettings ProfileSettings { get; set; } = null!;
    public Action<string?>? OnProfileSettingsChanged { get; set; }
    
    public ProfileSettingsService(AppStateService appStateService,
        ISender sender)
    {
        _appStateService = appStateService;
        _sender = sender;
        OnProfileSettingsChanged += UpdateProfileSettings;
    }
    
    public async Task LoadSettings()
    {
        var getSettings = await _sender.Send(new GetProfileSettings.Query(_appStateService.ProfileId));
        ProfileSettings = getSettings.IsSuccess ? getSettings.Value : new() {ProfileId = _appStateService.ProfileId};
        if (ProfileSettings.ProfileSettingsId == Guid.Empty)
        {
            await _sender.Send(new AddProfileSettings.Command(ProfileSettings));
        }
    }

    private async void UpdateProfileSettings(string? propertyName)
    {
        await _sender.Send(new UpdateProfileSettings.Command(ProfileSettings));
    }

    public void Dispose()
    {
        OnProfileSettingsChanged -= UpdateProfileSettings;
        GC.SuppressFinalize(this);
    }
}