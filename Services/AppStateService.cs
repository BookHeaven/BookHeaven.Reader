namespace BookHeaven.Reader.Services;

public class AppStateService
{
    public Action<string>? OnProfileNameChanged;
    
    public string? ServerUrl
    {
        get => Get<string>(nameof(ServerUrl));
        set => Set(nameof(ServerUrl), value);
    }
    
    public string? Language
    {
        get => Get<string>(nameof(Language));
        set => Set(nameof(Language), value);
    }
    
    public Guid ProfileId
    {
        get => Get<Guid>(nameof(ProfileId));
        set => Set(nameof(ProfileId), value);
    }
    
    private static void Set<T>(string key, T value)
    {
        Preferences.Set(key, value!.ToString());
    }

    private static T? Get<T>(string key)
    {
        object? value = Preferences.Get(key, null);
        var type = typeof(T);
        return type switch
        {
            _ when type == typeof(Guid) => (T)(object)Guid.Parse(value?.ToString() ?? Guid.Empty.ToString()),
            _ => (T?)value
        };
    }
}