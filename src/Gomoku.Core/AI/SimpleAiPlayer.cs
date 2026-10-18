using System.Diagnostics;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Fast heuristic Gomoku AI ported from Carbon Gomoku's AISimple.
/// Performs non-searching pattern scoring and immediate threat blocking.
/// </summary>
public sealed class SimpleAiPlayer : IPlayer
{
    private static readonly int[] DX = [1, 0, 1, 1];
    private static readonly int[] DY = [0, 1, 1, -1];
    private readonly Random _rng = new();

    public string Name => "Simple AI";
    public SearchAnalysis? LastAnalysis { get; private set; }

    public async ValueTask<Move> GetMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        IProgress<SearchAnalysis>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (legalMoves.Count == 0)
        {
            throw new InvalidOperationException("No legal moves available.");
        }

        Stopwatch sw = Stopwatch.StartNew();

        // 1. Center opening on empty board
        if (state.MoveCount == 0)
        {
            Point center = state.Size.Center;
            Move centerMove = new(center.X, center.Y, state.SideToMove);
            LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, 0, centerMove, 1, 0, sw.Elapsed, "Center opening", IsSearching: false);
            progress?.Report(LastAnalysis);
            if (progress != null)
            {
                await Task.Delay(250, cancellationToken);
            }
            return centerMove;
        }

        int bestVal = int.MinValue;
        Move bestMove = legalMoves[0];
        long nodes = 0;
        long lastReportMs = 0;

        PieceColor me = state.SideToMove;
        PieceColor opp = me.Opponent();

        foreach (Move move in legalMoves)
        {
            nodes++;
            if (!IsCandidate(state, move.X, move.Y)) continue;

            int valA = EvalCell(state, move.X, move.Y, me);
            int valB = EvalCell(state, move.X, move.Y, opp);
            int val = valA + valB + _rng.Next(4);

            if (valA >= 100000) // Immediate win!
            {
                bestMove = move;
                bestVal = valA;
                break;
            }

            if (valB >= 2000) val = 80000; // Opponent has open three - block it!
            if (valA >= 2000) val = 90000; // I have open three - play it!

            if (val > bestVal)
            {
                bestVal = val;
                bestMove = move;
            }

            if (progress != null && sw.ElapsedMilliseconds - lastReportMs >= 150)
            {
                lastReportMs = sw.ElapsedMilliseconds;
                progress.Report(new SearchAnalysis(
                    state.MoveCount + 1,
                    1,
                    bestVal,
                    bestMove,
                    nodes,
                    lastReportMs > 0 ? nodes * 1000 / lastReportMs : 0,
                    sw.Elapsed,
                    "Searching...",
                    IsSearching: true));
                await Task.Delay(1, cancellationToken);
            }
        }

        sw.Stop();
        LastAnalysis = new SearchAnalysis(
            state.MoveCount + 1,
            1,
            bestVal,
            bestMove,
            nodes,
            sw.ElapsedMilliseconds > 0 ? nodes * 1000 / sw.ElapsedMilliseconds : 0,
            sw.Elapsed,
            null,
            IsSearching: false);
        progress?.Report(LastAnalysis);

        return bestMove;
    }

    private static bool IsCandidate(BoardState state, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                PieceColor p = state.GetCell(x + dx, y + dy);
                if (p is PieceColor.Black or PieceColor.White) return true;
            }
        }
        return false;
    }

    private static int EvalCell(BoardState state, int xp, int yp, PieceColor who)
    {
        int valTot = 0;

        for (int k = 0; k < 4; k++)
        {
            int dx = DX[k];
            int dy = DY[k];

            int space1 = 0;
            int x = xp + dx; int y = yp + dy;
            while (IsFree(state, x, y, who) && space1 < 4) { space1++; x += dx; y += dy; }

            int space2 = 0;
            x = xp - dx; y = yp - dy;
            while (IsFree(state, x, y, who) && space2 < 4) { space2++; x -= dx; y -= dy; }

            x = xp - dx * space2;
            y = yp - dy * space2;

            int n3 = 0;
            int valDir = 0;

            for (int i = 0; i < space1 + space2 - 3; i++)
            {
                int valOne5 = CountStonesIn5(state, x, y, dx, dy, who);
                x += dx; y += dy;

                if (valOne5 == 4) return 100000; // Can complete four / win
                if (valOne5 == 3) n3++;
                valDir += valOne5;
            }

            if (n3 >= 2) valDir = 50; // Open three threat
            valTot += valDir * valDir;
        }

        if (valTot >= 2000) return 2000;
        return valTot;
    }

    private static bool IsFree(BoardState state, int x, int y, PieceColor who)
    {
        PieceColor p = state.GetCell(x, y);
        return p == who || p == PieceColor.Empty;
    }

    private static int CountStonesIn5(BoardState state, int startX, int startY, int dx, int dy, PieceColor who)
    {
        int count = 0;
        int x = startX; int y = startY;
        for (int i = 0; i < 5; i++)
        {
            if (state.GetCell(x, y) == who) count++;
            x += dx; y += dy;
        }
        return count;
    }
}

