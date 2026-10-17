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
}

