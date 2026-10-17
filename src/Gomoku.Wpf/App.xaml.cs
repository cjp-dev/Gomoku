using System.Windows;
using Gomoku.App.ViewModels;
using Gomoku.Wpf.Services;

namespace Gomoku.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dialogService = new WpfDialogService();
        var fileService = new WpfGameFileService();
        var soundService = new WpfSoundService();
        var viewModel = new MainViewModel(dialogService, fileService, soundService);

        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }
}

