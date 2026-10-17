namespace Gomoku.Core.Models;

/// <summary>
/// Represents a move placed on the Gomoku board.
/// </summary>
public readonly record struct Move(int X, int Y, PieceColor Player)
{
    public Point Position => new(X, Y);

    public override string ToString() => $"{Position.ToCoordinateString()} ({Player})";
}

