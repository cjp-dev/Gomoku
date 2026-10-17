using System.Windows;
using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.App.ViewModels;
using Gomoku.Wpf.Views;

namespace Gomoku.Wpf.Services;

public sealed class WpfDialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public Task ShowMessageAsync(string title, string message)
    {
        if (Owner != null)
            MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        MessageBoxResult res = Owner != null
            ? MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

        return Task.FromResult(res == MessageBoxResult.Yes);
    }

    public Task<T?> ShowCustomDialogAsync<T>(object viewModel)
    {
        if (viewModel is SettingsViewModel settingsVm)
        {
            var window = new SettingsWindow { Owner = Owner, DataContext = settingsVm };
            bool? result = window.ShowDialog();
            return Task.FromResult((T?)(object?)result);
        }

        if (viewModel is AboutInfo aboutInfo)
        {
            var window = new AboutWindow { Owner = Owner, DataContext = aboutInfo };
            window.ShowDialog();
            return Task.FromResult(default(T));
        }

        return Task.FromResult(default(T));
    }
}

