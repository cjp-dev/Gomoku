using System.IO;
using System.Windows;
using Gomoku.App.Services;
using Microsoft.Win32;

namespace Gomoku.Wpf.Services;

public sealed class WpfGameFileService : IGameFileService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public async Task<string?> OpenGameFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Gomoku Game",
            Filter = "All Gomoku Games (*.sgf;*.gomoku;*.txt)|*.sgf;*.gomoku;*.txt|Smart Game Format (*.sgf)|*.sgf|Gomoku Notation (*.gomoku)|*.gomoku|All Files (*.*)|*.*",
            DefaultExt = ".sgf"
        };

        if (dialog.ShowDialog(Owner) != true) return null;

        return await File.ReadAllTextAsync(dialog.FileName);
    }

    public async Task<bool> SaveGameFileAsync(string defaultFileName, string content, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Gomoku Game",
            FileName = defaultFileName,
            Filter = filter,
            DefaultExt = ".sgf"
        };

        if (dialog.ShowDialog(Owner) != true) return false;

        await File.WriteAllTextAsync(dialog.FileName, content);
        return true;
    }
}

