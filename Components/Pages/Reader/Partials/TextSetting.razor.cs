using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class TextSetting
{
    [Parameter] public string Label { get; set; } = null!;

    [Parameter] public double Min { get; set; }

    [Parameter] public double Max { get; set; }

    [Parameter] public double Increment { get; set; } = 1;

    [Parameter] public double Value { get; set; }

    [Parameter] public EventCallback<double> ValueChanged { get; set; }

    private async Task AddAmount(double amount)
    {
        if ((amount > 0 && Value + amount <= Max) || (amount < 0 && Value + amount >= Min))
        {
            Value += amount;
            await ValueChanged.InvokeAsync(Value);
        }
    }
}