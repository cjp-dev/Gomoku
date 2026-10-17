using Gomoku.App.Services;

namespace Gomoku.Web.Services;

public sealed class BrowserDialogService : IDialogService
{
    private TaskCompletionSource<object?>? _tcs;

    public event Action? StateChanged;

    public bool IsVisible { get; private set; }
    public string Title { get; private set; } = "";
    public string Message { get; private set; } = "";
    public bool IsConfirm { get; private set; }
    public object? ActiveViewModel { get; private set; }

    public Task ShowMessageAsync(string title, string message)
    {
        CloseCurrent(null);
        _tcs = new TaskCompletionSource<object?>();
        Title = title;
        Message = message;
        IsConfirm = false;
        ActiveViewModel = null;
        IsVisible = true;
        StateChanged?.Invoke();
        return _tcs.Task;
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        CloseCurrent(null);
        _tcs = new TaskCompletionSource<object?>();
        Title = title;
        Message = message;
        IsConfirm = true;
        ActiveViewModel = null;
        IsVisible = true;
        StateChanged?.Invoke();
        var res = await _tcs.Task;
        return res is true;
    }

    public async Task<T?> ShowCustomDialogAsync<T>(object viewModel)
    {
        CloseCurrent(null);
        _tcs = new TaskCompletionSource<object?>();
        Title = "";
        Message = "";
        IsConfirm = false;
        ActiveViewModel = viewModel;
        IsVisible = true;
        StateChanged?.Invoke();
        var res = await _tcs.Task;
        return (T?)res;
    }

    public void Dismiss(object? result = null)
    {
        IsVisible = false;
        ActiveViewModel = null;
        StateChanged?.Invoke();
        _tcs?.TrySetResult(result);
        _tcs = null;
    }

    private void CloseCurrent(object? result)
    {
        _tcs?.TrySetResult(result);
        _tcs = null;
    }
}

