namespace BookHeaven.Reader.Services;

public class AppStateService
{
    public string? CurrentScreenSaverCoverPath { get; set; }
    
    
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
    
    public bool EnableStandbyCoverWorkaround
    {
        get => Get<bool>(nameof(EnableStandbyCoverWorkaround));
        set  {
/*#if ANDROID
            if (value) ScreenForegroundService.Start(Android.App.Application.Context);
            else ScreenForegroundService.Stop(Android.App.Application.Context);
#endif*/
            Set(nameof(EnableStandbyCoverWorkaround), value);
        }
    }
    
    private static void Set<T>(string key, T value)
    {
        Preferences.Set(key, value!.ToString());
    }

    private static T? Get<T>(string key)
    {
        var value = Preferences.Get(key, null);
        if(value == null) return default;
        var type = typeof(T);
        
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(type);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return (T?)converter.ConvertFromString(value);
        }
        
        return default;
    }
}