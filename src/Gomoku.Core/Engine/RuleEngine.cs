using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// Core implementation of Gomoku rules across Freestyle, Exact5, and Renju variants.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    public static RuleEngine Instance { get; } = new();

    private static readonly int[] DX = [1, 0, 1, 1];
    private static readonly int[] DY = [0, 1, 1, -1];

    public IReadOnlyList<Move> GetLegalMoves(BoardState state)
    {
        if (state.Status.IsGameOver()) return Array.Empty<Move>();

        List<Move> moves = new(state.RemainingVacantCells);

        PatternBoard? renjuBoard = null;
        if (state.Variant == GomokuVariant.Renju && state.SideToMove == state.FirstPlayer)
        {
            renjuBoard = BuildPatternBoard(state);
        }

        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                if (state.IsEmpty(x, y))
                {
                    if (renjuBoard != null && renjuBoard.IsForbidden(x, y, state.SideToMove))
                    {
                        continue; // Skip forbidden Renju squares for Black
                    }

                    moves.Add(new Move(x, y, state.SideToMove));
                }
            }
        }

        return moves;
    }

    public bool IsMoveLegal(BoardState state, Move move, out string? reason)
    {
        reason = null;

        if (state.Status.IsGameOver())
        {
            reason = "The game is already over.";
            return false;
        }

        if (move.Player != state.SideToMove)
        {
            reason = $"It is not {move.Player}'s turn.";
            return false;
        }

        if (!move.Position.IsValid(state.Size))
        {
            reason = $"Position {move.Position} is out of bounds.";
            return false;
        }

        if (state.IsOccupied(move.X, move.Y))
        {
            reason = $"Position {move.Position} is already occupied.";
            return false;
        }

        if (state.Variant == GomokuVariant.Renju && move.Player == state.FirstPlayer)
        {
            PatternBoard pb = BuildPatternBoard(state);
            if (pb.IsForbidden(move.X, move.Y, move.Player))
            {
                reason = $"Move at {move.Position} is a forbidden foul in Renju (Double-Three, Double-Four, or Overline).";
                return false;
            }
        }

        return true;
    }

    public BoardState ApplyMove(BoardState state, Move move)
    {
        if (!IsMoveLegal(state, move, out string? reason))
        {
            throw new InvalidOperationException($"Illegal move: {reason}");
        }

        BoardState next = state.Clone();
        next.SetCell(move.X, move.Y, move.Player);
        next.MoveCount++;
        next.Hash ^= Zobrist.GetKey(move.Player, move.X, move.Y) ^ Zobrist.SideToMoveKey;

        if (CheckWin(next, move.X, move.Y, out IReadOnlyList<Point> winningLine))
        {
            next.Status = move.Player == PieceColor.Black ? GameStatus.BlackWon : GameStatus.WhiteWon;
            next.WinningLine = winningLine;
        }
        else if (next.MoveCount >= next.Size.TotalCells)
        {
            next.Status = GameStatus.Draw;
        }
        else
        {
            next.SideToMove = next.SideToMove.Opponent();
        }

        return next;
    }

    public bool CheckWin(BoardState state, int lastX, int lastY, out IReadOnlyList<Point> winningLine)
    {
        winningLine = Array.Empty<Point>();
        PieceColor player = state.GetCell(lastX, lastY);
        if (player is not (PieceColor.Black or PieceColor.White)) return false;

        for (int k = 0; k < 4; k++)
        {
            int dx = DX[k];
            int dy = DY[k];

            int c1 = 0;
            int x = lastX + dx;
            int y = lastY + dy;
            while (state.GetCell(x, y) == player)
            {
                c1++;
                x += dx;
                y += dy;
            }

            int c2 = 0;
            x = lastX - dx;
            y = lastY - dy;
            while (state.GetCell(x, y) == player)
            {
                c2++;
                x -= dx;
                y -= dy;
            }

            int count = c1 + c2 + 1;
            bool isWin = false;

            if (state.Variant == GomokuVariant.Freestyle)
            {
                isWin = count >= 5;
            }
            else if (state.Variant == GomokuVariant.Exact5)
            {
                isWin = count == 5;
            }
            else // Renju
            {
                if (player == state.FirstPlayer) // Black
                {
                    isWin = count == 5; // Black wins only on exact 5 (overlines are fouls)
                }
                else // White
                {
                    isWin = count >= 5; // White wins on 5 or more
                }
            }

            if (isWin)
            {
                int startX = lastX - c2 * dx;
                int startY = lastY - c2 * dy;
                Point[] line = new Point[count];
                for (int i = 0; i < count; i++)
                {
                    line[i] = new Point(startX + i * dx, startY + i * dy);
                }
                winningLine = line;
                return true;
            }
        }

        return false;
    }

    private static PatternBoard BuildPatternBoard(BoardState state)
    {
        PatternBoard pb = new();
        pb.Start(state.Size.Width, state.Size.Height, state.Variant, state.FirstPlayer);

        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                PieceColor piece = state.GetCell(x, y);
                if (piece is PieceColor.Black or PieceColor.White)
                {
                    pb.Who = piece;
                    pb.MakeMove(x, y);
                }
            }
        }

        pb.Who = state.SideToMove;
        return pb;
    }
}

