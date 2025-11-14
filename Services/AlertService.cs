using BookHeaven.Domain.Abstractions;
using BookHeaven.Domain.Enums;
using CommunityToolkit.Maui.Alerts;

namespace BookHeaven.Reader.Services;

public class AlertService : IAlertService
{
    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        await Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, cancel);
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return await Application.Current!.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel);
    }

    public async Task<string> ShowPromptAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return await Application.Current!.Windows[0].Page!.DisplayPromptAsync(title, message, accept, cancel,maxLength:100);
    }
    public async Task ShowToastAsync(string message, AlertSeverity alertSeverity = AlertSeverity.Info)
    {
        await Toast.Make(message).Show();
    }
}