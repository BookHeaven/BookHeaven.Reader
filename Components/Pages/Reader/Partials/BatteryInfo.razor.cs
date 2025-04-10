namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class BatteryInfo : IDisposable
{
    private int _batteryLevel = 100;
    private BatteryState _batteryState = BatteryState.Unknown;

    protected override void OnInitialized()
    {
        RefreshBatteryInfo(Battery.Default.ChargeLevel, Battery.Default.State);
        Battery.Default.BatteryInfoChanged += OnBatteryInfoChanged;
    }

    private void OnBatteryInfoChanged(object? sender, BatteryInfoChangedEventArgs e)
    {
        RefreshBatteryInfo(e.ChargeLevel, e.State);
    }

    private void RefreshBatteryInfo(double chargeLevel, BatteryState state)
    {
        _batteryLevel = (int)(chargeLevel * 100);
        _batteryState = state;

        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Battery.Default.BatteryInfoChanged -= OnBatteryInfoChanged;
        GC.SuppressFinalize(this);
    }
}