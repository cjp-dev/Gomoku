using System.Diagnostics;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("--gen-suite"))
        {
            double target = double.Parse(SuiteBenchmark.Arg(args, "--target") ?? "1.0", System.Globalization.CultureInfo.InvariantCulture);
            double signature = double.Parse(SuiteBenchmark.Arg(args, "--signature") ?? "0.03", System.Globalization.CultureInfo.InvariantCulture);
            await SuiteBenchmark.GenerateAsync(target, signature);
            return;
        }

        if (args.Contains("--update-signature"))
        {
            await SuiteBenchmark.UpdateSignatureAsync();
            return;
        }

        if (args.Contains("--tactical"))
        {
            await SuiteBenchmark.TacticalAsync(args);
            return;
        }

        if (args.Contains("--suite"))
        {
            await SuiteBenchmark.MainAsync(args);
            return;
        }

        if (args.Contains("--compare"))
        {
            int i = Array.IndexOf(args, "--compare");
            SuiteBenchmark.Compare(args[i + 1], args[i + 2]);
            return;
        }

        if (args.Contains("--ab"))
        {
            await AbMatch.MainAsync(args);
            return;
        }

        if (args.Contains("--inspect"))
        {
            double timePerMove = 0.2;
            int timeIdx = Array.IndexOf(args, "--time");
            if (timeIdx >= 0 && timeIdx + 1 < args.Length && double.TryParse(args[timeIdx + 1], System.Globalization.CultureInfo.InvariantCulture, out double t))
            {
                timePerMove = t;
            }

            int? fixedDepth = null;
            int depthIdx = Array.IndexOf(args, "--depth");
            if (depthIdx >= 0 && depthIdx + 1 < args.Length && int.TryParse(args[depthIdx + 1], out int d))
            {
                fixedDepth = d;
            }

            // Positions: 0 (Pos #1 Kagetsu), 1 (Pos #2 Hokuto), 4 (Pos #5 Ungetsu), 20 (Pos #21 Draw), 23 (Pos #24)
            List<int> targetIndices = [0, 1, 4, 20, 23];
            await MatchInspector.InspectPositionsAsync(targetIndices, timePerMove, fixedDepth);
            return;
        }

        if (args.Contains("--match"))
        {
            double timePerMove = 1.0;
            int timeIdx = Array.IndexOf(args, "--time");
            if (timeIdx >= 0 && timeIdx + 1 < args.Length && double.TryParse(args[timeIdx + 1], System.Globalization.CultureInfo.InvariantCulture, out double t))
            {
                timePerMove = t;
            }

            int openings = 20;
            int openIdx = Array.IndexOf(args, "--openings");
            if (openIdx >= 0 && openIdx + 1 < args.Length && int.TryParse(args[openIdx + 1], out int o))
            {
                openings = o;
            }

            await MatchRunner.RunTournamentAsync(timePerMove, openings);
            return;
        }

        if (args.Contains("--efficiency"))
        {
            int maxDepth = 10;
            int depthIdx = Array.IndexOf(args, "--depth");
            if (depthIdx >= 0 && depthIdx + 1 < args.Length && int.TryParse(args[depthIdx + 1], out int d))
            {
                maxDepth = d;
            }

            int threads = 1;
            int threadIdx = Array.IndexOf(args, "--threads");
            if (threadIdx >= 0 && threadIdx + 1 < args.Length && int.TryParse(args[threadIdx + 1], out int th))
            {
                threads = th;
            }

            await SearchEfficiencyBenchmark.RunAsync(maxDepth, threads);
            return;
        }

        bool quick = args.Contains("--quick");
        Console.WriteLine("=================================================");
        Console.WriteLine("       Gomoku .NET - Engine Benchmark Tool       ");
        Console.WriteLine($" Mode: {(quick ? "Quick Regression" : "Full Performance")}");
        Console.WriteLine("=================================================\n");

        BenchmarkPatternBoard(quick);
        BenchmarkBitboardPatternBoard(quick);
        BenchmarkBitboard15(quick);
        BenchmarkTranspositionTable(quick);
        BenchmarkLockFreeTT(quick);
        await BenchmarkCarbonSearchAsync(quick);

        Console.WriteLine("\n[Benchmark Complete] All tests passed successfully.");
    }

    private static void BenchmarkPatternBoard(bool quick)
    {
        Console.WriteLine("--- 1. PatternBoard Throughput & Move/Undo Speed ---");
        int iterations = quick ? 10_000 : 100_000;

        PatternBoard pb = new();
        pb.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            pb.MakeMove(7, 7);
            pb.MakeMove(7, 8);
            pb.MakeMove(8, 7);
            pb.UndoMove();
            pb.UndoMove();
            pb.UndoMove();
        }
        sw.Stop();

        long ops = iterations * 6;
        double opsPerSec = ops / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Executed {ops:N0} make/undo operations in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec).");
        Console.WriteLine($"Board Consistency: {(pb.CheckConsistency() ? "PASS" : "FAIL")}\n");
    }

    private static void BenchmarkBitboardPatternBoard(bool quick)
    {
        Console.WriteLine("--- 2. BitboardPatternBoard (1D Stride-128 + Chebyshev Dilation) ---");
        int iterations = quick ? 10_000 : 100_000;

        BitboardPatternBoard pb = new();
        pb.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            pb.MakeMove(7, 7);
            pb.MakeMove(7, 8);
            pb.MakeMove(8, 7);
            pb.UndoMove();
            pb.UndoMove();
            pb.UndoMove();
        }
        sw.Stop();

        long ops = iterations * 6;
        double opsPerSec = ops / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Executed {ops:N0} make/undo operations in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec).");
        Console.WriteLine($"Board Consistency: {(pb.CheckConsistency() ? "PASS" : "FAIL")}\n");
    }

    private static void BenchmarkBitboard15(bool quick)
    {
        Console.WriteLine("--- 3. Bitboard15 SIMD Throughput & Move/Undo Speed ---");
        int iterations = quick ? 10_000 : 100_000;

        Bitboard15 bb = new();

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            bb.MakeMove(7, 7);
            bb.MakeMove(7, 8);
            bb.MakeMove(8, 7);
            bb.UndoMove();
            bb.UndoMove();
            bb.UndoMove();
        }
        sw.Stop();

        long ops = iterations * 6;
        double opsPerSec = ops / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Executed {ops:N0} bitboard make/undo operations in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec).\n");
    }

    private static void BenchmarkTranspositionTable(bool quick)
    {
        Console.WriteLine("--- 4. Carbon Transposition Table (16-byte packed) ---");
        int[] sizes = quick ? [65_536, 1_048_576] : [65_536, 262_144, 1_048_576, 4_194_304];

        foreach (int size in sizes)
        {
            TranspositionTable tt = new(size);
            int operations = quick ? 100_000 : 1_000_000;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < operations; i++)
            {
                ulong key = (ulong)i * 0x9E3779B97F4A7C15UL;
                tt.Store(key, 4, 100, BoundType.Exact, new Point(7, 7));
                _ = tt.TryProbe(key, out _);
            }
            sw.Stop();

            double opsPerSec = (operations * 2) / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"TT Capacity: {tt.Capacity:N0} entries | {operations * 2:N0} ops in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec) | Hits: {tt.Hits:N0}");
        }
        Console.WriteLine();
    }

    private static void BenchmarkLockFreeTT(bool quick)
    {
        Console.WriteLine("--- 5. Lock-Free Transposition Table (Concurrent Atomic XOR) ---");
        int size = quick ? 262_144 : 1_048_576;
        int operations = quick ? 100_000 : 1_000_000;
        int[] threadConfigs = [1, Math.Min(4, Environment.ProcessorCount)];

        foreach (int threads in threadConfigs)
        {
            LockFreeTranspositionTable tt = new(size);
            int opsPerThread = operations / threads;

            Stopwatch sw = Stopwatch.StartNew();
            Parallel.For(0, threads, threadId =>
            {
                ulong baseKey = (ulong)threadId * 1_000_000;
                for (int i = 0; i < opsPerThread; i++)
                {
                    ulong key = (baseKey + (ulong)i) * 0x9E3779B97F4A7C15UL;
                    tt.Store(key, 4, 100, BoundType.Exact, new Point(7, 7));
                    _ = tt.TryProbe(key, out _, out _, out _, out _);
                }
            });
            sw.Stop();

            double opsPerSec = (operations * 2) / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"LockFree TT ({threads} Threads) | {operations * 2:N0} ops in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec) | Capacity: {tt.Capacity:N0}");
        }
        Console.WriteLine();
    }

    private static async Task BenchmarkCarbonSearchAsync(bool quick)
    {
        Console.WriteLine("--- 6. Carbon AI Search Throughput & Multi-Thread Scaling (NPS) ---");
        int depth = quick ? 4 : 6;
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        RuleEngine rules = RuleEngine.Instance;

        // Seed a standard opening: Black H8, White H9, Black I8
        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(7, 8, PieceColor.White));
        state = rules.ApplyMove(state, new Move(8, 7, PieceColor.Black));
        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(depth, useOpeningBook: false);

        // 1. Baseline Carbon (1T)
        {
            BaselineCarbonAiPlayer baseline = new();
            Stopwatch sw = Stopwatch.StartNew();
            Move move = await baseline.GetMoveAsync(state, limits, legal);
            sw.Stop();
            long nodes = baseline.LastAnalysis?.Nodes ?? 0;
            double nps = nodes / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            Console.WriteLine($"Baseline Carbon (1T)         | Depth: {depth} | Move: {move.Position} | Nodes: {nodes:N0} | Time: {sw.ElapsedMilliseconds} ms | NPS: {nps:N0} (1.00x)");
        }

        // 2. Production CarbonBitboardAiPlayer across thread configs
        List<int> threadConfigs = [1, 2, 4];
        if (Environment.ProcessorCount >= 8 && !threadConfigs.Contains(8))
        {
            threadConfigs.Add(8);
        }

        double baselineNps = 0;
        foreach (int threads in threadConfigs)
        {
            CarbonBitboardAiPlayer ai = new(threads);
            Stopwatch sw = Stopwatch.StartNew();
            Move move = await ai.GetMoveAsync(state, limits, legal);
            sw.Stop();

            long nodes = ai.LastAnalysis?.Nodes ?? 0;
            double nps = nodes / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            if (threads == 1) baselineNps = nps;
            double scaling = baselineNps > 0 ? nps / baselineNps : 1.0;

            string label = $"Carbon Bitboard ({threads}T)" + (threads == 1 ? "          " : threads < 10 ? "          " : "         ");
            Console.WriteLine($"{label.Substring(0, 28)} | Depth: {depth} | Move: {move.Position} | Nodes: {nodes:N0} | Time: {sw.ElapsedMilliseconds} ms | NPS: {nps:N0} ({scaling:F2}x vs 1T)");
        }
    }
}
