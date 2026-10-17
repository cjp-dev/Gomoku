using Gomoku.App.Models;
using Gomoku.App.Services;

namespace Gomoku.App.Tests.Mocks;

public sealed class MockDialogService : IDialogService
{
    public List<string> MessagesShown { get; } = [];
    public bool ConfirmationResult { get; set; } = true;
    public object? CustomDialogResult { get; set; }
    public Action<object>? OnShowCustomDialog { get; set; }

    public Task ShowMessageAsync(string title, string message)
    {
        MessagesShown.Add($"{title}: {message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        return Task.FromResult(ConfirmationResult);
    }

    public Task<T?> ShowCustomDialogAsync<T>(object viewModel)
    {
        OnShowCustomDialog?.Invoke(viewModel);
        return Task.FromResult((T?)CustomDialogResult);
    }
}

public sealed class MockGameFileService : IGameFileService
{
    public string? FileToOpen { get; set; }
    public string? SavedContent { get; set; }
    public string? SavedFileName { get; set; }

    public Task<string?> OpenGameFileAsync() => Task.FromResult(FileToOpen);

    public Task<bool> SaveGameFileAsync(string defaultFileName, string content, string filter)
    {
        SavedFileName = defaultFileName;
        SavedContent = content;
        return Task.FromResult(true);
    }
}

public sealed class MockSoundService : ISoundService
{
    public List<SoundType> PlayedSounds { get; } = [];
    public bool IsMuted { get; set; }

    public Task PlaySoundAsync(SoundType sound)
    {
        if (!IsMuted)
        {
            PlayedSounds.Add(sound);
        }
        return Task.CompletedTask;
    }
}

