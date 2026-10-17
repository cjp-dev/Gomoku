using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// Abstraction for Gomoku game rules, legal move generation, move application, and terminal condition detection.
/// </summary>
public interface IRuleEngine
{
    IReadOnlyList<Move> GetLegalMoves(BoardState state);

    bool IsMoveLegal(BoardState state, Move move, out string? reason);

    BoardState ApplyMove(BoardState state, Move move);

    bool CheckWin(BoardState state, int lastX, int lastY, out IReadOnlyList<Point> winningLine);
}

