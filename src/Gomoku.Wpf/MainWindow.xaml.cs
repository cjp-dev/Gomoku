using System.Windows;
using Gomoku.App.ViewModels;

namespace Gomoku.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDocsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/clauspedersen/gomoku#rules--documentation",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}

