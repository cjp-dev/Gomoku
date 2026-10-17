using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gomoku.Core.Models;

namespace Gomoku.App.ViewModels;

/// <summary>
/// ViewModel representing an individual intersection point on the Gomoku board grid.
/// </summary>
public sealed partial class IntersectionViewModel : ObservableObject
{
    private readonly Action<IntersectionViewModel>? _onClick;

    public int X { get; }
    public int Y { get; }
    public Point Point => new(X, Y);
    public string Coordinate => Point.ToCoordinateString();

    [ObservableProperty]
    private PieceColor _piece = PieceColor.Empty;

    [ObservableProperty]
    private bool _isStarPoint;

    [ObservableProperty]
    private bool _isLastMove;

    [ObservableProperty]
    private bool _isWinningLine;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private PieceColor _ghostPiece = PieceColor.Empty;

    [ObservableProperty]
    private int _moveNumber;

    [ObservableProperty]
    private bool _isForbidden;

    public bool HasPiece => Piece is PieceColor.Black or PieceColor.White;
    public bool IsBlack => Piece == PieceColor.Black;
    public bool IsWhite => Piece == PieceColor.White;

    public IRelayCommand ClickCommand { get; }

    public IntersectionViewModel(int x, int y, bool isStarPoint = false, Action<IntersectionViewModel>? onClick = null)
    {
        X = x;
        Y = y;
        IsStarPoint = isStarPoint;
        _onClick = onClick;
        ClickCommand = new RelayCommand(() => _onClick?.Invoke(this));
    }

    partial void OnPieceChanged(PieceColor value)
    {
        OnPropertyChanged(nameof(HasPiece));
        OnPropertyChanged(nameof(IsBlack));
        OnPropertyChanged(nameof(IsWhite));
    }
}

