namespace Gomoku.App.Services;

/// <summary>
/// Platform-agnostic file open and save dialogs for SGF and Gomoku records.
/// </summary>
public interface IGameFileService
{
    Task<string?> OpenGameFileAsync();

    Task<bool> SaveGameFileAsync(string defaultFileName, string content, string filter);
}

