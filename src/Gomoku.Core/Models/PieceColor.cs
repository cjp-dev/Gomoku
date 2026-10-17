namespace Gomoku.Core.Models;

/// <summary>
/// Represents the piece color or status of an intersection on the Gomoku board.
/// Black = 0 (plays first, OP in Carbon), White = 1 (plays second, XP in Carbon),
/// Empty = 2, Wrong = 3 (out-of-bounds border margin).
/// </summary>
public enum PieceColor : byte
{
    Black = 0,
    White = 1,
    Empty = 2,
    Wrong = 3
}

public static class PieceColorExtensions
{
    public static PieceColor Opponent(this PieceColor color) => color switch
    {
        PieceColor.Black => PieceColor.White,
        PieceColor.White => PieceColor.Black,
        _ => color
    };

    public static string Symbol(this PieceColor color) => color switch
    {
        PieceColor.Black => "●",
        PieceColor.White => "○",
        _ => "·"
    };

    public static string Name(this PieceColor color) => color switch
    {
        PieceColor.Black => "Black",
        PieceColor.White => "White",
        PieceColor.Empty => "Empty",
        _ => "Wrong"
    };
}

