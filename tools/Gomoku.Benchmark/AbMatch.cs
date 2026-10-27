using System.Diagnostics;
using System.Globalization;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

/// <summary>
/// <c>--ab</c>: a paired match between two option sets of <see cref="CarbonBitboardAiPlayer"/> in the same build.
/// Every opening is played twice with colours swapped; games run in parallel.
/// <code>--ab --a "pvs=1" --b "default" --games 400 --time 0.2 --threads 1 --parallel 8 [--variant Freestyle]</code>
/// </summary>
public static class AbMatch
{
    public static async Task MainAsync(string[] args)
    {
        CarbonSearchOptions a = CarbonSearchOptions.Parse(SuiteBenchmark.Arg(args, "--a"));
        CarbonSearchOptions b = CarbonSearchOptions.Parse(SuiteBenchmark.Arg(args, "--b"));
        int games = int.Parse(SuiteBenchmark.Arg(args, "--games") ?? "400", CultureInfo.InvariantCulture);
        double time = double.Parse(SuiteBenchmark.Arg(args, "--time") ?? "0.2", CultureInfo.InvariantCulture);
        int threads = int.Parse(SuiteBenchmark.Arg(args, "--threads") ?? "1", CultureInfo.InvariantCulture);
        int parallel = int.Parse(SuiteBenchmark.Arg(args, "--parallel") ?? Math.Max(1, 8 / threads).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        GomokuVariant variant = Enum.Parse<GomokuVariant>(SuiteBenchmark.Arg(args, "--variant") ?? "Freestyle");

        List<List<Point>> openings = Openings(games / 2);
        Console.WriteLine($"A: {a}\nB: {b}\n{openings.Count * 2} games, {time:F2} s/move, {threads} thread(s), {parallel} in parallel, {variant}");

        int aWins = 0, bWins = 0, draws = 0, done = 0;
        double depthA = 0, depthB = 0;
        long npsA = 0, npsB = 0, movesA = 0, movesB = 0;
        object gate = new();
        Stopwatch sw = Stopwatch.StartNew();

        var jobs = openings.SelectMany((o, i) => new[] { (Opening: o, Index: i, AIsBlack: true), (Opening: o, Index: i, AIsBlack: false) });
        await Parallel.ForEachAsync(jobs, new ParallelOptions { MaxDegreeOfParallelism = parallel }, async (job, _) =>
        {
            var g = await PlayAsync(variant, job.Opening, a, b, job.AIsBlack, time, threads);
            lock (gate)
            {
                done++;
                if (g.Winner == null) draws++;
                else if (g.Winner == true) aWins++;
                else bWins++;
                depthA += g.DepthA; depthB += g.DepthB; npsA += g.NpsA; npsB += g.NpsB; movesA += g.MovesA; movesB += g.MovesB;
                if (done % 20 == 0 || done == openings.Count * 2)
                {
                    Console.WriteLine($"{done,4} games: A {aWins} - B {bWins} - draws {draws} | {Summary(aWins, bWins, draws)} | {sw.Elapsed:mm\\:ss}");
                }
            }
        });

        Console.WriteLine();
        Console.WriteLine($"Result: A {aWins} - B {bWins} - draws {draws}; {Summary(aWins, bWins, draws)}");
        Console.WriteLine($"Average depth A {depthA / Math.Max(1, movesA):F2}, B {depthB / Math.Max(1, movesB):F2}; average NPS A {npsA / Math.Max(1, movesA):N0}, B {npsB / Math.Max(1, movesB):N0}");
    }

    /// <summary>Score of A with a 95% confidence interval and the Elo difference.</summary>
    public static string Summary(int aWins, int bWins, int draws)
    {
        int n = aWins + bWins + draws;
        if (n == 0) return "-";
        double s = (aWins + 0.5 * draws) / n;
        double w = (double)aWins / n, d = (double)draws / n;
        double variance = w + 0.25 * d - s * s; // per-game score variance
        double margin = 1.96 * Math.Sqrt(Math.Max(variance, 0) / n);
        return $"score {s:P1} ± {margin:P1}, Elo {Elo(s):+0;-0;0} [{Elo(s - margin):+0;-0;0}, {Elo(s + margin):+0;-0;0}]";
    }

    private static double Elo(double s)
    {
        s = Math.Clamp(s, 0.001, 0.999);
        return -400 * Math.Log10(1 / s - 1);
    }

    private sealed record Game(bool? Winner, double DepthA, double DepthB, long NpsA, long NpsB, int MovesA, int MovesB);

    private static async Task<Game> PlayAsync(GomokuVariant variant, List<Point> opening, CarbonSearchOptions a, CarbonSearchOptions b, bool aIsBlack, double time, int threads)
    {
        BoardState state = SuiteBenchmark.ToState(variant, opening);
        CarbonBitboardAiPlayer engineA = new(threads) { Options = a };
        CarbonBitboardAiPlayer engineB = new(threads) { Options = b };
        SearchLimits limits = SearchLimits.TimePerMove(TimeSpan.FromSeconds(time), useOpeningBook: false);

        double depthA = 0, depthB = 0;
        long npsA = 0, npsB = 0;
        int movesA = 0, movesB = 0;

        while (!state.Status.IsGameOver() && state.MoveCount < 120)
        {
            var legal = RuleEngine.Instance.GetLegalMoves(state);
            if (legal.Count == 0) break;

            bool aToMove = (state.SideToMove == PieceColor.Black) == aIsBlack;
            CarbonBitboardAiPlayer engine = aToMove ? engineA : engineB;
            Move m = await engine.GetMoveAsync(state, limits, legal);
            SearchAnalysis? an = engine.LastAnalysis;
            if (aToMove) { movesA++; depthA += an?.Depth ?? 0; npsA += an?.EvaluationsPerSec ?? 0; }
            else { movesB++; depthB += an?.Depth ?? 0; npsB += an?.EvaluationsPerSec ?? 0; }

            state = RuleEngine.Instance.ApplyMove(state, m);
        }

        bool? winner = state.Status switch
        {
            GameStatus.BlackWon => aIsBlack,
            GameStatus.WhiteWon => !aIsBlack,
            _ => null,
        };
        return new Game(winner, depthA, depthB, npsA, npsB, movesA, movesB);
    }

    /// <summary>
    /// The 26 canonical three-stone openings, each extended by every fourth stone (White) next to the
    /// first three stones, taken in a fixed spread order so a smaller count still covers all openings.
    /// </summary>
    public static List<List<Point>> Openings(int count)
    {
        var baseOpenings = MatchRunner.GenerateOpeningPositions(26);
        List<List<List<Point>>> perOpening = [];
        foreach (var o in baseOpenings)
        {
            List<List<Point>> extended = [];
            for (int y = 4; y <= 10; y++)
            {
                for (int x = 4; x <= 10; x++)
                {
                    Point p = new(x, y);
                    if (o.Contains(p)) continue;
                    if (o.Any(s => Math.Max(Math.Abs(s.X - x), Math.Abs(s.Y - y)) == 1))
                    {
                        extended.Add([.. o, p]);
                    }
                }
            }
            perOpening.Add(extended);
        }

        List<List<Point>> result = [];
        for (int round = 0; result.Count < count; round++)
        {
            bool any = false;
            foreach (var list in perOpening)
            {
                if (round < list.Count)
                {
                    any = true;
                    result.Add(list[round]);
                    if (result.Count == count) break;
                }
            }
            if (!any) break;
        }
        return result;
    }
}
