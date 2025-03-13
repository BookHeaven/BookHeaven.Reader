namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class BatteryInfo : IDisposable
{
    private string _batteryIcon = "battery-empty";
    private int _batteryLevel = 100;

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

        switch (state)
        {
            case BatteryState.Charging:
                _batteryIcon = "battery-charging";
                break;
            case BatteryState.Discharging:
            case BatteryState.Full:
                if (_batteryLevel >= 85)
                    _batteryIcon = "battery-full";
                else if (_batteryLevel >= 65)
                    _batteryIcon = "battery-three-quarters";
                else if (_batteryLevel >= 35)
                    _batteryIcon = "battery-half";
                else if (_batteryLevel >= 10)
                    _batteryIcon = "battery-quarter";
                else
                    _batteryIcon = "battery-empty";
                break;
        }

        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Battery.Default.BatteryInfoChanged -= OnBatteryInfoChanged;
        GC.SuppressFinalize(this);
    }
}