namespace BookHeaven.Reader.Services;

public class AppStateService
{
    public string? ServerUrl
    {
        get => Get(nameof(ServerUrl)) as string;
        set => Set(nameof(ServerUrl), value);
    }
    
    public string? Language
    {
        get => Get(nameof(Language)) as string;
        set => Set(nameof(Language), value);
    }
    
    public Guid ProfileId
    {
        get => Guid.TryParse(Get(nameof(ProfileId))?.ToString(), out var result) ? result : Guid.Empty;
        set => Set(nameof(ProfileId), value.ToString());
    }
    
    private void Set(string key, dynamic? value)
    {
        Preferences.Set(key, value);
    }

    private object? Get(string key)
    {
        return Preferences.Get(key, null);
    }
}