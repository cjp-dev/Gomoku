using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.App.ViewModels;
using Gomoku.Web.Components;
using GameShell;

namespace Gomoku.Web.Services;

/// <summary>
/// Modal dialogs shown one at a time by the DialogHost component, matching Checkers.Web.
/// </summary>
public sealed class BrowserDialogService(ShellDialogs dialogs) : IDialogService
{
    public async Task ShowMessageAsync(string title, string message)
    {
        await ShowAsync(new DialogRequest
        {
            Title = title,
            Message = message,
            Buttons = ["OK"]
        });
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        int button = await ShowAsync(new DialogRequest
        {
            Title = title,
            Message = message,
            Buttons = [confirmText, cancelText]
        });
        return button == 0;
    }

    public async Task<T?> ShowCustomDialogAsync<T>(object viewModel)
    {
        if (viewModel is SettingsViewModel settingsVm)
        {
            int button = await ShowAsync(new DialogRequest
            {
                Title = "Settings",
                Body = SettingsForm.For(settingsVm),
                Buttons = ["OK", "Cancel"]
            });

            if (button == 0)
            {
                settingsVm.SaveCommand.Execute(null);
                return (T?)(object?)true;
            }
            settingsVm.CancelCommand.Execute(null);
            return (T?)(object?)false;
        }

        if (viewModel is AboutInfo about)
        {
            await ShowAsync(new DialogRequest
            {
                Title = about.Title,
                Heading = $"Version {about.Version}",
                Picture = "images/claus-pedersen.jpg",
                PictureCaption = $"{about.Author}\nAuthor & Developer",
                Paragraphs = [
                    about.Description,
                    "Engine inspiration & algorithms: Michał Czardybon (Carbon Gomoku). Developed in C# and .NET 10."
                ],
                Buttons = ["OK"]
            });
            return (T?)(object?)true;
        }

        return default;
    }

    private Task<int> ShowAsync(DialogRequest request) => dialogs.ShowAsync(request);
}
