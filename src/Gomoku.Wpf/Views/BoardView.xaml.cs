using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Gomoku.App.ViewModels;
using Gomoku.Core.Models;

namespace Gomoku.Wpf.Views;

public partial class BoardView : UserControl
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public BoardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RedrawGrid();
        Loaded += (_, _) => RedrawGrid();
    }

    private void RedrawGrid()
    {
        GridCanvas.Children.Clear();
        LabelsCanvas.Children.Clear();

        if (ViewModel == null) return;

        BoardSize size = ViewModel.Settings.BoardSize;
        int cols = size.Width;
        int rows = size.Height;

        double boardSize = 640.0;
        double margin = 30.0;
        double innerSize = boardSize - 2 * margin;

        double cellW = innerSize / cols;
        double cellH = innerSize / rows;

        double startX = margin + 0.5 * cellW;
        double endX = margin + (cols - 0.5) * cellW;
        double startY = margin + 0.5 * cellH;
        double endY = margin + (rows - 0.5) * cellH;

        var lineBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x30, 0x18));
        var starBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x22, 0x10));
        var textBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x38, 0x18));

        // Horizontal grid lines
        for (int y = 0; y < rows; y++)
        {
            double cy = margin + (y + 0.5) * cellH;
            bool isBorder = y == 0 || y == rows - 1;
            Line line = new()
            {
                X1 = startX,
                Y1 = cy,
                X2 = endX,
                Y2 = cy,
                Stroke = lineBrush,
                StrokeThickness = isBorder ? 2.0 : 1.2
            };
            GridCanvas.Children.Add(line);

            // Row labels 1..N
            if (ViewModel.Settings.CoordinatesVisible)
            {
                AddLabel(LabelsCanvas, (y + 1).ToString(), 12, cy - 8, textBrush);
                AddLabel(LabelsCanvas, (y + 1).ToString(), 628, cy - 8, textBrush);
            }
        }

        // Vertical grid lines
        for (int x = 0; x < cols; x++)
        {
            double cx = margin + (x + 0.5) * cellW;
            bool isBorder = x == 0 || x == cols - 1;
            Line line = new()
            {
                X1 = cx,
                Y1 = startY,
                X2 = cx,
                Y2 = endY,
                Stroke = lineBrush,
                StrokeThickness = isBorder ? 2.0 : 1.2
            };
            GridCanvas.Children.Add(line);

            // Column labels A..T
            if (ViewModel.Settings.CoordinatesVisible)
            {
                char colChar = (char)('A' + x);
                AddLabel(LabelsCanvas, colChar.ToString(), cx - 5, 8, textBrush);
                AddLabel(LabelsCanvas, colChar.ToString(), cx - 5, 622, textBrush);
            }
        }

        // Star points
        foreach (var sp in size.GetStarPoints())
        {
            double cx = margin + (sp.X + 0.5) * cellW;
            double cy = margin + (sp.Y + 0.5) * cellH;
            Ellipse dot = new()
            {
                Width = 7,
                Height = 7,
                Fill = starBrush
            };
            Canvas.SetLeft(dot, cx - 3.5);
            Canvas.SetTop(dot, cy - 3.5);
            GridCanvas.Children.Add(dot);
        }
    }

    private static void AddLabel(Canvas canvas, string text, double left, double top, Brush brush)
    {
        TextBlock tb = new()
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = brush
        };
        Canvas.SetLeft(tb, left);
        Canvas.SetTop(tb, top);
        canvas.Children.Add(tb);
    }

    private void OnCellMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IntersectionViewModel vm } && !vm.HasPiece && ViewModel != null && ViewModel.Settings.GhostStoneEnabled)
        {
            vm.IsHovered = true;
        }
    }

    private void OnCellMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IntersectionViewModel vm })
        {
            vm.IsHovered = false;
        }
    }
}

