namespace Gomoku.App.Services;

/// <summary>
/// Platform-agnostic dialog service for alert messages, confirmations, and custom modals.
/// </summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");

    Task<T?> ShowCustomDialogAsync<T>(object viewModel);
}

