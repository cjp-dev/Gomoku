using System.Diagnostics;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Benchmark;

/// <summary>
/// Diagnostic Inspector that plays paired matches between two engines and logs move-by-move telemetry,
/// detecting the exact ply where move decisions diverge between New Carbon AI and Baseline Carbon AI.
/// </summary>
public static class MatchInspector
{
    public record DivergenceReport(
        int PositionIndex,
        string OpeningName,
        int GameNumber,
        string EngineAName,
        string EngineBName,
        PieceColor Winner,
        int TotalPlies,
        int FirstDivergencePly,
        Move? DivergentMoveA,
        Move? DivergentMoveB,
        SearchAnalysis? AnalysisA,
        SearchAnalysis? AnalysisB,
        string DivergenceExplanation,
        List<string> FullMoveHistoryGomokuNotation);

    public static string ToGomokuCoord(int x, int y)
    {
        char col = (char)('A' + (x >= 8 ? x + 1 : x)); // Skip 'I' in Gomoku/Renju convention, or standard letter
        // Standard Gomoku coordinates: A..O (or A..Z), row 1..15 from bottom up
        // Here x in 0..14 is A..O (or skipping I). Let's use standard A-O:
        char colLetter = (char)('A' + x);
        int rowNumber = 15 - y;
        return $"{colLetter}{rowNumber}";
    }

    public static string ToGomokuCoord(Point p) => ToGomokuCoord(p.X, p.Y);

    public static async Task<List<DivergenceReport>> InspectPositionsAsync(
        List<int> targetIndices,
        double timePerMoveSec = 0.2,
        int? fixedDepth = null)
    {
        var allOpenings = MatchRunner.GenerateOpeningPositions(26);
        List<DivergenceReport> reports = [];

        string[] openingNames =
        [
            "D1: Kagetsu (Flower Moon)",
            "D2: Hokuto (North Star)",
            "D3: Daigetsu (Great Moon)",
            "D4: Ryusei (Shooting Star)",
            "D5: Ungetsu (Cloud Moon)",
            "D6: Urano (Deep Rain)",
            "D7: Keigetsu (Valley Moon)",
            "D8: Sangetsu (Mountain Moon)",
            "D9: Kangetsu (Cold Moon)",
            "D10: Meigetsu (Bright Moon)",
            "D11: Chosei (Long Star)",
            "D12: Suisei (Water Star)",
            "D13: Seigetsu (Clear Moon)",
            "I1: Shogetsu (Pine Moon)",
            "I2: Shagetsu (Slanted Moon)",
            "I3: Hoko (Tiger)",
            "I4: Kyogetsu (Slight Moon)",
            "I5: Suigetsu (Water Moon)",
            "I6: Gyokusei (Jade Star)",
            "I7: Hakushu (White Autumn)",
            "I8: Ryusei (Dragon Star)",
            "I9: Zangetsu (Morning Moon)",
            "I10: Shinsei (New Star)",
            "I11: Myosei (Morning Star)",
            "I12: Ryusei 2",
            "I13: Kousei (Constant Star)"
        ];

        foreach (int idx in targetIndices)
        {
            if (idx < 0 || idx >= allOpenings.Count) continue;
            var opening = allOpenings[idx];
            string opName = idx < openingNames.Length ? openingNames[idx] : $"Opening #{idx + 1}";

            Console.WriteLine("\n=======================================================");
            Console.WriteLine($" INSPECTING POSITION #{idx + 1}: {opName}");
            Console.WriteLine($" Mode: {(fixedDepth.HasValue ? $"Fixed Depth {fixedDepth.Value}" : $"Fixed Time {timePerMoveSec:F2}s")}");
            Console.WriteLine("=======================================================");

            // Game 1: Engine A = Black, Engine B = White
            var rep1 = await PlayAndTraceGameAsync(
                idx + 1, opName, 1,
                () => new CarbonAiPlayer(threadCount: 1),
                () => new BaselineCarbonAiPlayer(),
                opening, timePerMoveSec, fixedDepth);
            reports.Add(rep1);

            // Game 2: Engine B = Black, Engine A = White
            var rep2 = await PlayAndTraceGameAsync(
                idx + 1, opName, 2,
                () => new BaselineCarbonAiPlayer(),
                () => new CarbonAiPlayer(threadCount: 1),
                opening, timePerMoveSec, fixedDepth);
            reports.Add(rep2);
        }

        return reports;
    }

    private static async Task<DivergenceReport> PlayAndTraceGameAsync(
        int positionIdx,
        string openingName,
        int gameNumber,
        Func<IPlayer> createBlack,
        Func<IPlayer> createWhite,
        List<Point> opening,
        double timePerMoveSec,
        int? fixedDepth)
    {
        IPlayer blackPlayer = createBlack();
        IPlayer whitePlayer = createWhite();

        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        RuleEngine rules = RuleEngine.Instance;
        List<string> moveHistory = [];

        foreach (var p in opening)
        {
            Move m = new(p.X, p.Y, state.SideToMove);
            state = rules.ApplyMove(state, m);
            moveHistory.Add(ToGomokuCoord(p));
        }

        SearchLimits limits = fixedDepth.HasValue
            ? SearchLimits.FixedDepth(fixedDepth.Value, useOpeningBook: false)
            : SearchLimits.TimePerMove(TimeSpan.FromSeconds(timePerMoveSec), useOpeningBook: false);

        int firstDivPly = -1;
        Move? divMoveA = null;
        Move? divMoveB = null;
        SearchAnalysis? divAnalysisA = null;
        SearchAnalysis? divAnalysisB = null;
        string explanation = "Moves were identical throughout";

        const int maxPlies = 120;

        while (!state.Status.IsGameOver() && state.MoveCount < maxPlies)
        {
            var legalMoves = rules.GetLegalMoves(state);
            if (legalMoves.Count == 0) break;

            int currentPly = state.MoveCount + 1;
            PieceColor side = state.SideToMove;

            // Probe what BOTH engines would choose at this exact board state!
            IPlayer pA = new CarbonAiPlayer(threadCount: 1);
            IPlayer pB = new BaselineCarbonAiPlayer();

            Move choiceA = await pA.GetMoveAsync(state, limits, legalMoves);
            Move choiceB = await pB.GetMoveAsync(state, limits, legalMoves);

            if (firstDivPly == -1 && choiceA.Position != choiceB.Position)
            {
                firstDivPly = currentPly;
                divMoveA = choiceA;
                divMoveB = choiceB;
                divAnalysisA = pA.LastAnalysis;
                divAnalysisB = pB.LastAnalysis;

                explanation = $"Ply {currentPly} ({side}): New Carbon chose {ToGomokuCoord(choiceA.Position)} (D:{pA.LastAnalysis?.Depth}, Score:{pA.LastAnalysis?.Score}, T:{pA.LastAnalysis?.Elapsed.TotalMilliseconds:F0}ms), " +
                              $"Baseline chose {ToGomokuCoord(choiceB.Position)} (D:{pB.LastAnalysis?.Depth}, Score:{pB.LastAnalysis?.Score}, T:{pB.LastAnalysis?.Elapsed.TotalMilliseconds:F0}ms)";

                Console.WriteLine($"  [!] FIRST DIVERGENCE at Ply {currentPly} ({side}):");
                Console.WriteLine($"      New Carbon AI (1T): {ToGomokuCoord(choiceA.Position)} [Depth {pA.LastAnalysis?.Depth}, Eval {pA.LastAnalysis?.Score}, Time {pA.LastAnalysis?.Elapsed.TotalMilliseconds:F0}ms, Nodes {pA.LastAnalysis?.Nodes:N0}]");
                Console.WriteLine($"      Baseline Carbon AI: {ToGomokuCoord(choiceB.Position)} [Depth {pB.LastAnalysis?.Depth}, Eval {pB.LastAnalysis?.Score}, Time {pB.LastAnalysis?.Elapsed.TotalMilliseconds:F0}ms, Nodes {pB.LastAnalysis?.Nodes:N0}]");
            }

            // The actual player for this turn makes its move
            IPlayer active = state.SideToMove == PieceColor.Black ? blackPlayer : whitePlayer;
            Move actualMove = await active.GetMoveAsync(state, limits, legalMoves);
            state = rules.ApplyMove(state, actualMove);
            moveHistory.Add(ToGomokuCoord(actualMove.Position));
        }

        PieceColor winner = state.Status switch
        {
            GameStatus.BlackWon => PieceColor.Black,
            GameStatus.WhiteWon => PieceColor.White,
            _ => PieceColor.Empty
        };

        Console.WriteLine($"  Game {gameNumber} Finished: Winner = {winner} ({state.MoveCount} plies)");

        return new DivergenceReport(
            positionIdx,
            openingName,
            gameNumber,
            blackPlayer.Name,
            whitePlayer.Name,
            winner,
            state.MoveCount,
            firstDivPly,
            divMoveA,
            divMoveB,
            divAnalysisA,
            divAnalysisB,
            explanation,
            moveHistory);
    }
}
