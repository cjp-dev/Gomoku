using System.Diagnostics;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

/// <summary>
/// Automated Head-to-Head Tournament Match Runner for comparing Gomoku AI engines.
/// Executes paired matches from canonical opening leaves under fixed time-per-move controls.
/// </summary>
public static class MatchRunner
{
    public record MatchResult(
        string MatchName,
        string EngineAName,
        string EngineBName,
        int GamesPlayed,
        int EngineAWins,
        int EngineBWins,
        int Draws,
        double EngineAWinRate,
        double EngineBAvgDepth,
        double EngineAAvgDepth,
        long EngineBAvgNps,
        long EngineAAvgNps,
        TimeSpan TotalDuration);

    public static async Task RunTournamentAsync(double timePerMoveSec = 1.0, int openingCount = 20)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   Gomoku .NET - Engine Head-to-Head Tournament  ");
        Console.WriteLine($"   Time Limit: {timePerMoveSec:F1}s / move");
        Console.WriteLine($"   Opening Positions: {openingCount} (Paired: {openingCount * 2} games per match)");
        Console.WriteLine("=================================================\n");

        var openings = GenerateOpeningPositions(openingCount);

        int multiThreads = Math.Max(2, Math.Min(8, Environment.ProcessorCount));

        // ----------------------------------------------------
        // MATCH 2A: Bitboard Single-Threaded Match (1T vs 1T)
        // ----------------------------------------------------
        Console.WriteLine("=================================================");
        Console.WriteLine(">>> MATCH 2A: New Carbon AI Bitboard (1T) vs Carbon AI (1T) <<<");
        Console.WriteLine("=================================================");
        var match2A = await RunPairedMatchAsync(
            matchName: "Bitboard Single-Threaded Match (1T vs 1T)",
            createEngineA: () => new CarbonBitboardAiPlayer(threadCount: 1),
            createEngineB: () => new CarbonAiPlayer(threadCount: 1),
            openings: openings,
            timePerMoveSec: timePerMoveSec);

        PrintMatchReport(match2A);

        // ----------------------------------------------------
        // MATCH 2B: Bitboard Multithreaded Match (8T vs 8T)
        // ----------------------------------------------------
        Console.WriteLine("=================================================");
        Console.WriteLine($">>> MATCH 2B: New Carbon AI Bitboard ({multiThreads}T) vs Carbon AI ({multiThreads}T) <<<");
        Console.WriteLine("=================================================");
        var match2B = await RunPairedMatchAsync(
            matchName: $"Bitboard Multithreaded Match ({multiThreads}T vs {multiThreads}T)",
            createEngineA: () => new CarbonBitboardAiPlayer(threadCount: multiThreads),
            createEngineB: () => new CarbonAiPlayer(threadCount: multiThreads),
            openings: openings,
            timePerMoveSec: timePerMoveSec);

        PrintMatchReport(match2B);

        // Final Summary
        Console.WriteLine("\n=================================================");
        Console.WriteLine("                TOURNAMENT SUMMARY               ");
        Console.WriteLine("=================================================");
        Console.WriteLine($"Match 2A (1T vs 1T): Carbon Bitboard {match2A.EngineAWins} - {match2A.EngineBWins} Carbon AI ({match2A.Draws} draws) | Win Rate: {match2A.EngineAWinRate:P1}");
        Console.WriteLine($"Match 2B ({multiThreads}T vs {multiThreads}T): Carbon Bitboard {match2B.EngineAWins} - {match2B.EngineBWins} Carbon AI ({match2B.Draws} draws) | Win Rate: {match2B.EngineAWinRate:P1}");
        Console.WriteLine("=================================================\n");
        Console.WriteLine("=================================================\n");
    }

    private static async Task<MatchResult> RunPairedMatchAsync(
        string matchName,
        Func<IPlayer> createEngineA,
        Func<IPlayer> createEngineB,
        List<List<Point>> openings,
        double timePerMoveSec)
    {
        int aWins = 0;
        int bWins = 0;
        int draws = 0;
        int games = 0;

        double sumDepthA = 0;
        double sumDepthB = 0;
        long sumNpsA = 0;
        long sumNpsB = 0;
        long movesA = 0;
        long movesB = 0;

        Stopwatch matchSw = Stopwatch.StartNew();

        for (int i = 0; i < openings.Count; i++)
        {
            var opening = openings[i];
            Console.WriteLine($"\n--- Position #{i + 1}/{openings.Count} [Moves: {opening.Count}] ---");

            // Game 1: Engine A is Black, Engine B is White
            IPlayer engineA1 = createEngineA();
            IPlayer engineB1 = createEngineB();
            var g1 = await PlayGameAsync(engineA1, engineB1, opening, timePerMoveSec);
            games++;
            RecordGameStats(g1.Winner, isAFirst: true, ref aWins, ref bWins, ref draws);
            sumDepthA += g1.AvgDepthBlack; movesA += g1.MovesBlack; sumNpsA += g1.AvgNpsBlack;
            sumDepthB += g1.AvgDepthWhite; movesB += g1.MovesWhite; sumNpsB += g1.AvgNpsWhite;
            Console.WriteLine($"Game 1 (A=Black vs B=White): Winner: {g1.Winner} ({g1.TotalPlies} plies, {g1.Duration.TotalSeconds:F1}s)");

            // Game 2: Engine B is Black, Engine A is White (Inverted)
            IPlayer engineA2 = createEngineA();
            IPlayer engineB2 = createEngineB();
            var g2 = await PlayGameAsync(engineB2, engineA2, opening, timePerMoveSec);
            games++;
            RecordGameStats(g2.Winner, isAFirst: false, ref aWins, ref bWins, ref draws);
            sumDepthB += g2.AvgDepthBlack; movesB += g2.MovesBlack; sumNpsB += g2.AvgNpsBlack;
            sumDepthA += g2.AvgDepthWhite; movesA += g2.MovesWhite; sumNpsA += g2.AvgNpsWhite;
            Console.WriteLine($"Game 2 (B=Black vs A=White): Winner: {g2.Winner} ({g2.TotalPlies} plies, {g2.Duration.TotalSeconds:F1}s)");
        }

        matchSw.Stop();

        double winRateA = (aWins + 0.5 * draws) / Math.Max(1, games);
        double avgDepthA = movesA > 0 ? sumDepthA / (games) : 0;
        double avgDepthB = movesB > 0 ? sumDepthB / (games) : 0;
        long avgNpsA = movesA > 0 ? sumNpsA / (games) : 0;
        long avgNpsB = movesB > 0 ? sumNpsB / (games) : 0;

        return new MatchResult(
            matchName,
            createEngineA().Name,
            createEngineB().Name,
            games,
            aWins,
            bWins,
            draws,
            winRateA,
            avgDepthB,
            avgDepthA,
            avgNpsB,
            avgNpsA,
            matchSw.Elapsed);
    }

    private static void RecordGameStats(PieceColor winner, bool isAFirst, ref int aWins, ref int bWins, ref int draws)
    {
        if (winner == PieceColor.Empty)
        {
            draws++;
        }
        else if (winner == PieceColor.Black)
        {
            if (isAFirst) aWins++; else bWins++;
        }
        else // White wins
        {
            if (isAFirst) bWins++; else aWins++;
        }
    }

    private record GameResult(
        PieceColor Winner,
        int TotalPlies,
        TimeSpan Duration,
        double AvgDepthBlack,
        double AvgDepthWhite,
        long AvgNpsBlack,
        long AvgNpsWhite,
        int MovesBlack,
        int MovesWhite);

    private static async Task<GameResult> PlayGameAsync(
        IPlayer blackPlayer,
        IPlayer whitePlayer,
        List<Point> openingMoves,
        double timePerMoveSec)
    {
        Stopwatch sw = Stopwatch.StartNew();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        RuleEngine rules = RuleEngine.Instance;

        // Apply opening moves
        foreach (var pt in openingMoves)
        {
            state = rules.ApplyMove(state, new Move(pt.X, pt.Y, state.SideToMove));
        }

        SearchLimits limits = SearchLimits.TimePerMove(TimeSpan.FromSeconds(timePerMoveSec));

        double sumDepthB = 0, sumDepthW = 0;
        long sumNpsB = 0, sumNpsW = 0;
        int movesB = 0, movesW = 0;

        const int maxPlies = 120; // Prevent runaway draws

        while (!state.Status.IsGameOver() && state.MoveCount < maxPlies)
        {
            var legalMoves = rules.GetLegalMoves(state);
            if (legalMoves.Count == 0) break;

            IPlayer active = state.SideToMove == PieceColor.Black ? blackPlayer : whitePlayer;
            Move move = await active.GetMoveAsync(state, limits, legalMoves);

            if (state.SideToMove == PieceColor.Black)
            {
                movesB++;
                sumDepthB += active.LastAnalysis?.Depth ?? 0;
                sumNpsB += active.LastAnalysis?.EvaluationsPerSec ?? 0;
            }
            else
            {
                movesW++;
                sumDepthW += active.LastAnalysis?.Depth ?? 0;
                sumNpsW += active.LastAnalysis?.EvaluationsPerSec ?? 0;
            }

            state = rules.ApplyMove(state, move);
        }

        sw.Stop();

        PieceColor winner = state.Status switch
        {
            GameStatus.BlackWon => PieceColor.Black,
            GameStatus.WhiteWon => PieceColor.White,
            _ => PieceColor.Empty
        };

        return new GameResult(
            winner,
            state.MoveCount,
            sw.Elapsed,
            movesB > 0 ? sumDepthB / movesB : 0,
            movesW > 0 ? sumDepthW / movesW : 0,
            movesB > 0 ? sumNpsB / movesB : 0,
            movesW > 0 ? sumNpsW / movesW : 0,
            movesB,
            movesW);
    }

    private static void PrintMatchReport(MatchResult res)
    {
        Console.WriteLine("\n-------------------------------------------------");
        Console.WriteLine($" REPORT: {res.MatchName}");
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine($"  {res.EngineAName}: {res.EngineAWins} wins ({res.EngineAWinRate:P1})");
        Console.WriteLine($"  {res.EngineBName}: {res.EngineBWins} wins");
        Console.WriteLine($"  Draws: {res.Draws}");
        Console.WriteLine($"  Total Games: {res.GamesPlayed} in {res.TotalDuration.TotalMinutes:F1} min");
        Console.WriteLine($"  {res.EngineAName} Avg Depth: {res.EngineAAvgDepth:F1} plies | Avg NPS: {res.EngineAAvgNps:N0}");
        Console.WriteLine($"  {res.EngineBName} Avg Depth: {res.EngineBAvgDepth:F1} plies | Avg NPS: {res.EngineBAvgNps:N0}");
        Console.WriteLine("-------------------------------------------------\n");
    }

    public static List<List<Point>> GenerateOpeningPositions(int count)
    {
        // 26 canonical openings and common 3-stone / 4-stone variations
        // Center is H8 = (7, 7)
        List<List<Point>> all =
        [
            // Direct Openings (H8 + H9)
            [new(7, 7), new(7, 8), new(8, 7)], // D1: Kagetsu (Flower Moon)
            [new(7, 7), new(7, 8), new(8, 8)], // D2: Hokuto (North Star)
            [new(7, 7), new(7, 8), new(8, 9)], // D3: Daigetsu (Great Moon)
            [new(7, 7), new(7, 8), new(8, 10)], // D4: Ryusei (Shooting Star)
            [new(7, 7), new(7, 8), new(7, 9)], // D5: Ungetsu (Cloud Moon)
            [new(7, 7), new(7, 8), new(7, 10)], // D6: Urano (Deep Rain)
            [new(7, 7), new(7, 8), new(6, 9)], // D7: Keigetsu (Valley Moon)
            [new(7, 7), new(7, 8), new(6, 8)], // D8: Sangetsu (Mountain Moon)
            [new(7, 7), new(7, 8), new(6, 7)], // D9: Kangetsu (Cold Moon)
            [new(7, 7), new(7, 8), new(6, 6)], // D10: Meigetsu (Bright Moon)
            [new(7, 7), new(7, 8), new(9, 7)], // D11: Chosei (Long Star)
            [new(7, 7), new(7, 8), new(9, 8)], // D12: Suisei (Water Star)
            [new(7, 7), new(7, 8), new(9, 9)], // D13: Seigetsu (Clear Moon)

            // Indirect Openings (H8 + I9)
            [new(7, 7), new(8, 8), new(6, 6)], // I1: Shogetsu (Pine Moon)
            [new(7, 7), new(8, 8), new(7, 6)], // I2: Shagetsu (Slanted Moon)
            [new(7, 7), new(8, 8), new(8, 6)], // I3: Hoko (Tiger)
            [new(7, 7), new(8, 8), new(9, 6)], // I4: Kyogetsu (Slight Moon)
            [new(7, 7), new(8, 8), new(6, 7)], // I5: Suigetsu (Water Moon)
            [new(7, 7), new(8, 8), new(7, 8)], // I6: Gyokusei (Jade Star)
            [new(7, 7), new(8, 8), new(8, 7)], // I7: Hakushu (White Autumn)
            [new(7, 7), new(8, 8), new(9, 7)], // I8: Ryusei (Dragon Star)
            [new(7, 7), new(8, 8), new(6, 8)], // I9: Zangetsu (Morning Moon)
            [new(7, 7), new(8, 8), new(6, 9)], // I10: Shinsei (New Star)
            [new(7, 7), new(8, 8), new(7, 9)], // I11: Myosei (Morning Star)
            [new(7, 7), new(8, 8), new(8, 9)], // I12: Ryusei 2
            [new(7, 7), new(8, 8), new(9, 9)], // I13: Kousei (Constant Star)

            // 4-stone variations
            [new(7, 7), new(7, 8), new(8, 7), new(6, 6)],
            [new(7, 7), new(7, 8), new(8, 7), new(8, 8)],
            [new(7, 7), new(7, 8), new(8, 7), new(9, 7)],
            [new(7, 7), new(7, 8), new(8, 7), new(7, 6)],
            [new(7, 7), new(7, 8), new(8, 8), new(6, 7)],
            [new(7, 7), new(7, 8), new(8, 8), new(8, 9)],
            [new(7, 7), new(7, 8), new(8, 9), new(6, 8)],
            [new(7, 7), new(7, 8), new(8, 9), new(7, 9)],
            [new(7, 7), new(7, 8), new(8, 9), new(9, 8)],
            [new(7, 7), new(8, 8), new(6, 6), new(8, 6)],
            [new(7, 7), new(8, 8), new(6, 6), new(7, 6)],
            [new(7, 7), new(8, 8), new(6, 6), new(6, 7)],
            [new(7, 7), new(8, 8), new(7, 6), new(8, 6)],
            [new(7, 7), new(8, 8), new(7, 6), new(9, 6)],
            [new(7, 7), new(8, 8), new(8, 6), new(6, 8)],
            [new(7, 7), new(8, 8), new(8, 6), new(7, 8)],
            [new(7, 7), new(8, 8), new(9, 6), new(8, 7)],
            [new(7, 7), new(8, 8), new(6, 7), new(7, 8)],
            [new(7, 7), new(8, 8), new(6, 7), new(6, 9)],
            [new(7, 7), new(8, 8), new(7, 8), new(9, 9)],
            [new(7, 7), new(8, 8), new(7, 8), new(6, 6)],
            [new(7, 7), new(8, 8), new(8, 7), new(9, 8)],
            [new(7, 7), new(8, 8), new(8, 7), new(6, 9)],
            [new(7, 7), new(8, 8), new(9, 7), new(8, 9)],
            [new(7, 7), new(8, 8), new(6, 8), new(7, 9)],
            [new(7, 7), new(8, 8), new(6, 9), new(8, 9)],
            [new(7, 7), new(8, 8), new(7, 9), new(9, 9)],
            [new(7, 7), new(7, 8), new(6, 9), new(7, 9)],
            [new(7, 7), new(7, 8), new(6, 9), new(5, 10)],
            [new(7, 7), new(7, 8), new(6, 8), new(7, 9)],
            [new(7, 7), new(7, 8), new(6, 8), new(5, 8)],
            [new(7, 7), new(7, 8), new(6, 7), new(7, 6)]
        ];

        return all.Take(Math.Min(count, all.Count)).ToList();
    }
}
