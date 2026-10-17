namespace Gomoku.Core.Models;

/// <summary>
/// Status of a Gomoku game.
/// </summary>
public enum GameStatus
{
    InProgress,
    BlackWon,
    WhiteWon,
    Draw
}

public static class GameStatusExtensions
{
    public static bool IsGameOver(this GameStatus status) => status != GameStatus.InProgress;

    public static PieceColor Winner(this GameStatus status) => status switch
    {
        GameStatus.BlackWon => PieceColor.Black,
        GameStatus.WhiteWon => PieceColor.White,
        _ => PieceColor.Empty
    };
}

