namespace BookHeaven.Reader.Services;

public class AppStateService
{
    public string? CurrentScreenSaverCoverPath { get; set; }
    
    
    public Action<string>? OnProfileNameChanged;
    
    public Action? OnTemperatureSettingsChanged;
    
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
    
    public bool EnableColorTemperatureAdjustment
    {
        get => Get<bool>(nameof(EnableColorTemperatureAdjustment));
        set
        {
            Set(nameof(EnableColorTemperatureAdjustment), value);
            OnTemperatureSettingsChanged?.Invoke();
        }
    }

    public int ColorTemperatureInKelvin
    {
        get => Get<int?>(nameof(ColorTemperatureInKelvin)) ?? 5000;
        set
        {
            Set(nameof(ColorTemperatureInKelvin), value);
            OnTemperatureSettingsChanged?.Invoke();
        }
    }

    public decimal ColorTemperatureOpacity
    {
        get => Get<decimal?>(nameof(ColorTemperatureOpacity)) ?? 1;
        set
        {
            Set(nameof(ColorTemperatureOpacity), value);
            OnTemperatureSettingsChanged?.Invoke();
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