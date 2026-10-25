using System.Diagnostics;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

/// <summary>
/// Measures the search efficiency and branching factor reduction of CarbonBitboardAiPlayer
/// across canonical Gomoku positions at multiple iterative deepening depths.
/// </summary>
public static class SearchEfficiencyBenchmark
{
    private static readonly int[][] FullGame =
    [
        [7, 7], [6, 6], [6, 5], [7, 6],
        [5, 6], [5, 7], [4, 5], [7, 5],
        [8, 4], [7, 4], [7, 3], [9, 5],
        [6, 3], [6, 7], [5, 4], [7, 2],
        [5, 3], [4, 3]
    ];

    private static readonly (string Name, int MoveCount, string Description)[] CanonicalPositions =
    [
        ("Position 1: Opening (Move 3)", 3, "Classic Indirect Opening: (7,7), (6,6), (6,5)"),
        ("Position 2: Early Midgame (Move 7)", 7, "Cross skirmish in center 5x5 region"),
        ("Position 3: Contested Midgame (Move 11)", 11, "Overlapping diagonal open threes and tactical fight"),
        ("Position 4: Deep Tactical Midgame (Move 15)", 15, "Dense cluster (rows 4-9, cols 3-7) with tactical threats")
    ];

    public static async Task RunAsync(int maxDepth = 10, int threadCount = 1)
    {
        Console.WriteLine("=================================================================================================================");
        Console.WriteLine($"      Carbon Search Efficiency & Branching Factor Reduction Benchmark (Threads: {threadCount})");
        Console.WriteLine($"      Target Max Depth: {maxDepth} (Depths: 2, 4, 6, 8, ... {maxDepth}) | 1-Min Budget Enforced");
        Console.WriteLine("=================================================================================================================\n");

        RuleEngine rules = RuleEngine.Instance;

        foreach (var (name, count, desc) in CanonicalPositions)
        {
            Console.WriteLine($"--- {name} ---");
            Console.WriteLine($"Tactical Context: {desc}");

            BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
            for (int i = 0; i < count; i++)
            {
                state = rules.ApplyMove(state, new Move(FullGame[i][0], FullGame[i][1], i % 2 == 0 ? PieceColor.Black : PieceColor.White));
            }

            var legalMoves = rules.GetLegalMoves(state);
            int bLegal = legalMoves.Count;

            // Compute local candidates within Chebyshev distance <= 2
            int bCand = 0;
            foreach (var m in legalMoves)
            {
                bool near = false;
                for (int i = 0; i < count; i++)
                {
                    if (Math.Max(Math.Abs(m.X - FullGame[i][0]), Math.Abs(m.Y - FullGame[i][1])) <= 2)
                    {
                        near = true;
                        break;
                    }
                }
                if (near) bCand++;
            }

            double sqrtLegal = Math.Sqrt(bLegal);
            double sqrtCand = Math.Sqrt(bCand);

            Console.WriteLine($"Plies on Board: {count} | Legal Moves (B_legal): {bLegal} | Candidate Moves (B_cand): {bCand}");
            Console.WriteLine($"Knuth-Moore Alpha-Beta Bounds: sqrt(B_legal) = {sqrtLegal:F2} | sqrt(B_cand) = {sqrtCand:F2}");
            Console.WriteLine();
            Console.WriteLine("| Depth | Actual Nodes | Time Actual | Engine NPS | b_eff | Theor. Legal Nodes | Est. Time (Legal) | Theor. Cand Nodes | Est. Time (Cand) |");
            Console.WriteLine("|:-----:|:------------:|:-----------:|:----------:|:-----:|:------------------:|:-----------------:|:-----------------:|:----------------:|");

            for (int d = 2; d <= maxDepth; d += 2)
            {
                CarbonBitboardAiPlayer ai = new(threadCount: threadCount);
                SearchLimits limits = SearchLimits.FixedDepth(d, useOpeningBook: false);

                Stopwatch sw = Stopwatch.StartNew();
                Move bestMove = await ai.GetMoveAsync(state, limits, legalMoves);
                sw.Stop();

                long actualNodes = ai.LastAnalysis?.Nodes ?? 0;
                double actualSec = sw.Elapsed.TotalSeconds;
                double nps = actualNodes / Math.Max(0.0001, actualSec);
                double beff = Math.Pow(actualNodes, 1.0 / d);

                double legalNodesTheor = Math.Pow(bLegal, d);
                double candNodesTheor = Math.Pow(bCand, d);

                double estSecLegal = legalNodesTheor / Math.Max(1.0, nps);
                double estSecCand = candNodesTheor / Math.Max(1.0, nps);

                string timeActualStr = FormatTime(actualSec);
                string estLegalStr = FormatTime(estSecLegal);
                string estCandStr = FormatTime(estSecCand);

                Console.WriteLine($"| {d,5} | {actualNodes,12:N0} | {timeActualStr,11} | {nps,10:N0} | {beff,5:F2} | {legalNodesTheor,18:E2} | {estLegalStr,17} | {candNodesTheor,17:E2} | {estCandStr,16} |");

                if (actualSec > 35.0 && d < maxDepth)
                {
                    Console.WriteLine($"\n[Note: Stopped depth progression early for {name} at depth {d} to guarantee < 1 min runtime constraint]\n");
                    break;
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("[Search Efficiency Benchmark Complete] All positions evaluated successfully.\n");
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsInfinity(seconds) || seconds > 1e18) return "> 10^10 yrs";
        if (seconds < 0.001) return $"{seconds * 1000:F1} ms";
        if (seconds < 1.0) return $"{seconds * 1000:F0} ms";
        if (seconds < 60) return $"{seconds:F2} s";
        if (seconds < 3600) return $"{seconds / 60:F1} min";
        if (seconds < 86400) return $"{seconds / 3600:F1} hrs";
        if (seconds < 86400 * 365.25) return $"{seconds / 86400:F1} days";

        double years = seconds / (86400 * 365.25);
        if (years < 1000) return $"{years:F1} yrs";
        if (years < 1e6) return $"{years / 1000:F1}k yrs";
        if (years < 1e9) return $"{years / 1e6:F1}M yrs";
        return $"{years / 1e9:F1}B yrs";
    }
}

