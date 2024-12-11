namespace BookHeaven.Reader.Services;

public class AlertService
{
    public Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        return Application.Current!.Windows[0].Page!.DisplayAlert(title, message, cancel);
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return Application.Current!.Windows[0].Page!.DisplayAlert(title, message, accept, cancel);
    }

    public Task<string> ShowPromptAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        return Application.Current!.Windows[0].Page!.DisplayPromptAsync(title, message, accept, cancel,maxLength:100);
    }
    
    public void ShowAlert(string title, string message, string cancel = "OK")
    {
        Application.Current!.Windows[0].Page!.Dispatcher.DispatchAsync(async () =>
            await ShowAlertAsync(title, message, cancel)
        );
    }
    public void ShowConfirmation(string title, string message, Action<bool> callback, string accept = "Yes", string cancel = "No")
    {
        Application.Current!.Windows[0].Page!.Dispatcher.DispatchAsync(async () =>
        {
            var answer = await ShowConfirmationAsync(title, message, accept, cancel);
            callback(answer);
        });
    }
}