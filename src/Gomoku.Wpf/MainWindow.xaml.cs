using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Gomoku.App.ViewModels;

namespace Gomoku.Wpf;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand UserGuideCommand = new(
        "User Guide", nameof(UserGuideCommand), typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F1) });

    public static readonly RoutedUICommand DocumentationCommand = new(
        "Rules & Documentation", nameof(DocumentationCommand), typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F1, ModifierKeys.Shift) });

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnUserGuideExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        OpenUrl("https://github.com/clauspedersen/gomoku/blob/main/docs/brain/14-gui-and-settings.md");
    }

    private void OnDocumentationExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        OpenUrl("https://github.com/clauspedersen/gomoku/blob/main/docs/brain/README.md");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}

