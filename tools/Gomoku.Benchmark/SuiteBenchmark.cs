using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

/// <summary>
/// The fixed-depth position suite used to measure engine speed and to check that a change plays the same:
/// <list type="bullet">
/// <item><c>--gen-suite</c> plays self-play games and writes the suite, the test signature and the tactical positions.</item>
/// <item><c>--suite</c> searches every position at its depth (1 thread, fresh table) several times and reports the medians.</item>
/// <item><c>--compare a.json b.json</c> compares two suite runs.</item>
/// </list>
/// </summary>
public static class SuiteBenchmark
{
    public sealed record SuitePosition(string Name, GomokuVariant Variant, int Depth, Point[] Moves);

    public sealed record PositionResult(string Name, int Depth, int CompletedDepth, long Nodes, double Ms, string Best, int Score, string? Stats);

    public sealed record SuiteRun(string Label, string Options, int Runs, List<PositionResult> Positions)
    {
        public long TotalNodes => Positions.Sum(p => p.Nodes);
        public double TotalMs => Positions.Sum(p => p.Ms);
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string SuitePath => FindRepoFile("tools", "Gomoku.Benchmark", "suite-positions.txt");
    public static string SignaturePath => FindRepoFile("tests", "Gomoku.Core.Tests", "TestData", "engine-signature.txt");
    public static string TacticalPath => FindRepoFile("tests", "Gomoku.Core.Tests", "TestData", "tactical-positions.txt");

    // ------------------------------------------------------------------ file format

    /// <summary>One position per line: <c>name|variant|depth|x,y x,y ...</c>; '#' starts a comment.</summary>
    public static List<SuitePosition> Load(string path) =>
        [.. File.ReadAllLines(path)
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(Parse)];

    public static SuitePosition Parse(string line)
    {
        string[] f = line.Split('|');
        Point[] moves = [.. f[3].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(m =>
        {
            string[] xy = m.Split(',');
            return new Point(int.Parse(xy[0], CultureInfo.InvariantCulture), int.Parse(xy[1], CultureInfo.InvariantCulture));
        })];
        return new SuitePosition(f[0], Enum.Parse<GomokuVariant>(f[1]), int.Parse(f[2], CultureInfo.InvariantCulture), moves);
    }

    public static string Format(SuitePosition p) =>
        $"{p.Name}|{p.Variant}|{p.Depth}|{string.Join(' ', p.Moves.Select(m => $"{m.X},{m.Y}"))}";

    public static BoardState ToState(GomokuVariant variant, IEnumerable<Point> moves)
    {
        BoardState state = new(BoardSize.Size15x15, variant);
        foreach (Point m in moves)
        {
            state = RuleEngine.Instance.ApplyMove(state, new Move(m.X, m.Y, state.SideToMove));
        }
        return state;
    }

    // ------------------------------------------------------------------ searching

    public static async Task<PositionResult> SearchAsync(SuitePosition p, CarbonSearchOptions options, bool collectStats, int threads = 1)
    {
        BoardState state = ToState(p.Variant, p.Moves);
        CarbonBitboardAiPlayer ai = new(threads) { Options = options, CollectStats = collectStats };
        var legal = RuleEngine.Instance.GetLegalMoves(state);

        long start = Stopwatch.GetTimestamp();
        Move best = await ai.GetMoveAsync(state, SearchLimits.FixedDepth(p.Depth, useOpeningBook: false), legal);
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        SearchAnalysis a = ai.LastAnalysis!;
        return new PositionResult(p.Name, p.Depth, a.Depth, a.Nodes, ms, $"{best.X},{best.Y}", a.Score, ai.LastStats?.ToString());
    }

    public static async Task<SuiteRun> RunAsync(string label, List<SuitePosition> suite, CarbonSearchOptions options, int runs, bool collectStats, int threads = 1)
    {
        // Warm-up: one shallow pass so the JIT has compiled the hot paths before timing.
        foreach (SuitePosition p in suite)
        {
            await SearchAsync(p with { Depth = Math.Min(p.Depth, 4) }, options, collectStats: false, threads);
        }

        var all = new List<PositionResult>[suite.Count];
        for (int i = 0; i < suite.Count; i++) all[i] = [];

        for (int r = 0; r < runs; r++)
        {
            for (int i = 0; i < suite.Count; i++)
            {
                all[i].Add(await SearchAsync(suite[i], options, collectStats && r == 0, threads));
            }
        }

        List<PositionResult> results = [];
        for (int i = 0; i < suite.Count; i++)
        {
            var list = all[i];
            if (threads == 1 && list.Select(x => (x.Nodes, x.Best, x.Score)).Distinct().Count() > 1)
            {
                throw new InvalidOperationException($"{suite[i].Name}: the search is not deterministic.");
            }

            double median = list.Select(x => x.Ms).Order().ElementAt(list.Count / 2);
            results.Add(list[0] with { Ms = median });
        }

        return new SuiteRun(label, options.ToString(), runs, results);
    }

    public static async Task MainAsync(string[] args)
    {
        string label = Arg(args, "--label") ?? "run";
        int runs = int.Parse(Arg(args, "--runs") ?? "5", CultureInfo.InvariantCulture);
        int threads = int.Parse(Arg(args, "--threads") ?? "1", CultureInfo.InvariantCulture);
        CarbonSearchOptions options = CarbonSearchOptions.Parse(Arg(args, "--options"));
        bool stats = args.Contains("--stats");
        string? only = Arg(args, "--only");

        List<SuitePosition> suite = Load(SuitePath);
        if (only != null) suite = [.. suite.Where(p => p.Name.Contains(only, StringComparison.OrdinalIgnoreCase))];

        Console.WriteLine($"Suite: {suite.Count} positions, {runs} runs, {threads} thread(s), options {options}");
        SuiteRun run = await RunAsync(label, suite, options, runs, stats, threads);
        Print(run);

        string? json = Arg(args, "--json");
        if (json != null)
        {
            File.WriteAllText(json, JsonSerializer.Serialize(run, Json));
            Console.WriteLine($"Saved {json}");
        }
    }

    public static void Print(SuiteRun run)
    {
        Console.WriteLine();
        Console.WriteLine($"| Position | Depth | Nodes | ms | NPS | Best | Score |");
        Console.WriteLine($"|---|--:|--:|--:|--:|--:|--:|");
        foreach (PositionResult p in run.Positions)
        {
            Console.WriteLine($"| {p.Name} | {p.CompletedDepth}/{p.Depth} | {p.Nodes:N0} | {p.Ms:F1} | {Nps(p.Nodes, p.Ms):N0} | {p.Best} | {p.Score} |");
            if (p.Stats != null) Console.WriteLine($"|   stats | {p.Stats} | | | | | |");
        }
        Console.WriteLine($"| **Total** | | **{run.TotalNodes:N0}** | **{run.TotalMs:F0}** | **{Nps(run.TotalNodes, run.TotalMs):N0}** | | |");
    }

    public static void Compare(string pathA, string pathB)
    {
        SuiteRun a = JsonSerializer.Deserialize<SuiteRun>(File.ReadAllText(pathA))!;
        SuiteRun b = JsonSerializer.Deserialize<SuiteRun>(File.ReadAllText(pathB))!;

        Console.WriteLine($"A = {a.Label} ({a.Options}), B = {b.Label} ({b.Options})");
        Console.WriteLine("| Position | Nodes A | Nodes B | Nodes B/A | ms A | ms B | ms B/A | Same result |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|");
        int same = 0;
        foreach (PositionResult pa in a.Positions)
        {
            PositionResult? pb = b.Positions.FirstOrDefault(x => x.Name == pa.Name);
            if (pb == null) continue;
            bool identical = pa.Nodes == pb.Nodes && pa.Best == pb.Best && pa.Score == pb.Score && pa.CompletedDepth == pb.CompletedDepth;
            if (identical) same++;
            Console.WriteLine($"| {pa.Name} | {pa.Nodes:N0} | {pb.Nodes:N0} | {Ratio(pb.Nodes, pa.Nodes)} | {pa.Ms:F1} | {pb.Ms:F1} | {Ratio(pb.Ms, pa.Ms)} | {(identical ? "yes" : "**no**")} |");
        }

        Console.WriteLine($"| **Total** | {a.TotalNodes:N0} | {b.TotalNodes:N0} | {Ratio(b.TotalNodes, a.TotalNodes)} | {a.TotalMs:F0} | {b.TotalMs:F0} | {Ratio(b.TotalMs, a.TotalMs)} | {same}/{a.Positions.Count} |");
        Console.WriteLine($"NPS A {Nps(a.TotalNodes, a.TotalMs):N0}, NPS B {Nps(b.TotalNodes, b.TotalMs):N0}; time change {(b.TotalMs / a.TotalMs - 1):+0.0%;-0.0%}");
    }

    // ------------------------------------------------------------------ generating the suite

    /// <summary>
    /// Plays deterministic self-play games (fixed depth 4) from canonical openings in all three variants,
    /// samples positions from them and picks a depth for each, then writes the suite, the signature for the
    /// tests and the tactical positions (forced wins found within a small depth).
    /// </summary>
    public static async Task GenerateAsync(double targetSec, double signatureSec)
    {
        var openings = MatchRunner.GenerateOpeningPositions(26);
        (GomokuVariant Variant, int[] Openings)[] plan =
        [
            (GomokuVariant.Freestyle, [0, 2, 4, 7, 13, 15, 18, 22]),
            (GomokuVariant.Exact5, [1, 9, 16, 24]),
            (GomokuVariant.Renju, [3, 11, 14, 20]),
        ];
        int[] samplePlies = [8, 14, 20, 26];

        List<SuitePosition> suite = [];
        List<SuitePosition> tactical = [];

        foreach (var (variant, openingIds) in plan)
        {
            foreach (int id in openingIds)
            {
                List<Point> game = await SelfPlayAsync(variant, openings[id]);
                BoardState end = ToState(variant, game);
                string gameName = $"{variant}-o{id + 1}";
                Console.WriteLine($"{gameName}: {game.Count} plies, {end.Status}");

                foreach (int ply in samplePlies)
                {
                    if (ply >= game.Count - 2) break;
                    var p = new SuitePosition($"{gameName}-p{ply}", variant, 4, [.. game.Take(ply)]);
                    PositionResult r4 = await SearchAsync(p, CarbonSearchOptions.Default, false);
                    if (Math.Abs(r4.Score) >= CarbonBitboardAiPlayer.WinMin) continue; // decided: tactical suite instead
                    suite.Add(p with { Depth = await PickDepthAsync(p, targetSec) });
                }

                if (end.Status is GameStatus.BlackWon or GameStatus.WhiteWon)
                {
                    // Winner to move, 2..5 own moves before the end.
                    for (int k = 2; k <= 5; k++)
                    {
                        int ply = game.Count - 1 - 2 * (k - 1);
                        if (ply < openings[id].Count + 2) break;
                        var p = new SuitePosition($"{gameName}-win{k}", variant, 2, [.. game.Take(ply)]);
                        int? d = await WinDepthAsync(p, maxDepth: 10, maxSec: 1.0);
                        if (d is int depth) tactical.Add(p with { Depth = depth });
                    }
                }
            }
        }

        File.WriteAllLines(SuitePath, ["# name|variant|depth|moves (x,y from the top-left, 0-based); generated by --gen-suite", .. suite.Select(Format)]);
        Console.WriteLine($"Wrote {suite.Count} suite positions to {SuitePath}");

        File.WriteAllLines(TacticalPath, ["# Forced wins: the side to move must report a win (score >= WinMin) at this depth; generated by --gen-suite", .. tactical.Select(Format)]);
        Console.WriteLine($"Wrote {tactical.Count} tactical positions to {TacticalPath}");

        List<SuitePosition> signature = [];
        foreach (SuitePosition p in suite)
        {
            signature.Add(p with { Depth = await PickDepthAsync(p with { Depth = 2 }, signatureSec) });
        }
        await WriteSignatureAsync(signature);
    }

    /// <summary>Recomputes the expected results in the signature file (after a deliberate change in play).</summary>
    public static async Task UpdateSignatureAsync()
    {
        List<SuitePosition> positions = [.. File.ReadAllLines(SignaturePath)
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(l => Parse(string.Join('|', l.Split('|').Take(4))))];
        await WriteSignatureAsync(positions);
    }

    private static async Task WriteSignatureAsync(List<SuitePosition> positions)
    {
        List<string> lines = ["# name|variant|depth|moves|best|score|nodes: 1 thread, fresh table, no book; checked by EngineSignatureTests"];
        foreach (SuitePosition p in positions)
        {
            PositionResult r = await SearchAsync(p, CarbonSearchOptions.Default, false);
            lines.Add($"{Format(p)}|{r.Best}|{r.Score}|{r.Nodes}");
        }
        File.WriteAllLines(SignaturePath, lines);
        Console.WriteLine($"Wrote {positions.Count} signature positions to {SignaturePath}");
    }

    private static async Task<List<Point>> SelfPlayAsync(GomokuVariant variant, List<Point> opening)
    {
        BoardState state = ToState(variant, opening);
        List<Point> moves = [.. opening];
        CarbonBitboardAiPlayer ai = new(1);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);

        while (!state.Status.IsGameOver() && state.MoveCount < 80)
        {
            ai.Table.Clear();
            var legal = RuleEngine.Instance.GetLegalMoves(state);
            if (legal.Count == 0) break;
            Move m = await ai.GetMoveAsync(state, limits, legal);
            moves.Add(new Point(m.X, m.Y));
            state = RuleEngine.Instance.ApplyMove(state, m);
        }

        return moves;
    }

    private static async Task<int> PickDepthAsync(SuitePosition p, double targetSec)
    {
        int chosen = p.Depth;
        for (int d = p.Depth; d <= 14; d++)
        {
            PositionResult r = await SearchAsync(p with { Depth = d }, CarbonSearchOptions.Default, false);
            if (r.Ms / 1000.0 > targetSec || r.CompletedDepth < d) break;
            chosen = d;
        }
        return chosen;
    }

    private static async Task<int?> WinDepthAsync(SuitePosition p, int maxDepth, double maxSec)
    {
        for (int d = 2; d <= maxDepth; d++)
        {
            PositionResult r = await SearchAsync(p with { Depth = d }, CarbonSearchOptions.Default, false);
            if (r.Score >= CarbonBitboardAiPlayer.WinMin) return d;
            if (r.Ms / 1000.0 > maxSec) return null;
        }
        return null;
    }

    // ------------------------------------------------------------------ helpers

    private static string FindRepoFile(params string[] parts)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Gomoku.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine([dir ?? throw new InvalidOperationException("Gomoku.slnx not found."), .. parts]);
    }

    public static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static long Nps(long nodes, double ms) => ms > 0 ? (long)(nodes * 1000 / ms) : 0;

    private static string Ratio(double b, double a) => a > 0 ? $"{b / a:F3}" : "-";
}
