using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.App.ViewModels;

namespace Gomoku.Web.Services;

/// <summary>
/// Modal dialogs shown one at a time by the DialogHost component, matching Checkers.Web.
/// </summary>
public sealed class BrowserDialogService : IDialogService
{
    private readonly Queue<DialogRequest> _queue = new();

    public event Action? Changed;

    public DialogRequest? Current => _queue.TryPeek(out DialogRequest? request) ? request : null;

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
                Settings = settingsVm,
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

    /// <summary>Closes the dialog if it is still the one shown.</summary>
    public void Close(DialogRequest request, int button)
    {
        if (Current != request) return;

        _queue.Dequeue();
        request.Complete(button);
        Changed?.Invoke();
    }

    private Task<int> ShowAsync(DialogRequest request)
    {
        _queue.Enqueue(request);
        Changed?.Invoke();
        return request.Result;
    }
}

/// <summary>The first button is the default (Enter), the last one cancels (Escape).</summary>
public sealed class DialogRequest
{
    private readonly TaskCompletionSource<int> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public required string Title { get; init; }
    public string? Heading { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Paragraphs { get; init; } = [];
    public string? Picture { get; init; }
    public string? PictureCaption { get; init; }
    public SettingsViewModel? Settings { get; init; }
    public string? Text { get; set; }
    public required IReadOnlyList<string> Buttons { get; init; }

    internal Task<int> Result => _result.Task;
    internal void Complete(int button) => _result.TrySetResult(button);
}
