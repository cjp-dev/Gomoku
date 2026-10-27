using System.Globalization;
using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.Core.Tests;

/// <summary>
/// Guards the engine's play: at a fixed depth (1 thread, fresh table, no book) every position must give
/// the recorded best move, score and node count. A pure speed-up keeps all three; a deliberate change in
/// the search updates TestData/engine-signature.txt with <c>Gomoku.Benchmark --update-signature</c>.
/// Also checks that the engine still finds the recorded forced wins.
/// </summary>
public class EngineSignatureTests
{
    public sealed record Position(string Name, GomokuVariant Variant, int Depth, Point[] Moves, string[] Expected);

    public static TheoryData<string> SignatureNames() => Names("engine-signature.txt");

    public static TheoryData<string> TacticalNames() => Names("tactical-positions.txt");

    [Theory]
    [MemberData(nameof(SignatureNames))]
    public async Task FixedDepthSearch_GivesTheRecordedMoveScoreAndNodes(string name)
    {
        Position p = Load("engine-signature.txt").Single(x => x.Name == name);

        (Move best, SearchAnalysis analysis) = await SearchAsync(p);

        $"{best.X},{best.Y}|{analysis.Score}|{analysis.Nodes}".Should().Be(string.Join('|', p.Expected),
            "a speed-up must not change the search; update the signature only for a deliberate change");
    }

    [Theory]
    [MemberData(nameof(TacticalNames))]
    public async Task ForcedWin_IsFoundAtTheRecordedDepth(string name)
    {
        Position p = Load("tactical-positions.txt").Single(x => x.Name == name);

        (_, SearchAnalysis analysis) = await SearchAsync(p);

        analysis.Score.Should().BeGreaterThanOrEqualTo(CarbonBitboardAiPlayer.WinMin, "the side to move has a forced win within this depth");
    }

    private static async Task<(Move, SearchAnalysis)> SearchAsync(Position p)
    {
        BoardState state = new(BoardSize.Size15x15, p.Variant);
        foreach (Point m in p.Moves)
        {
            state = RuleEngine.Instance.ApplyMove(state, new Move(m.X, m.Y, state.SideToMove));
        }

        CarbonBitboardAiPlayer ai = new(threadCount: 1);
        Move best = await ai.GetMoveAsync(state, SearchLimits.FixedDepth(p.Depth, useOpeningBook: false), RuleEngine.Instance.GetLegalMoves(state));
        return (best, ai.LastAnalysis!);
    }

    private static TheoryData<string> Names(string file)
    {
        TheoryData<string> data = [];
        foreach (Position p in Load(file)) data.Add(p.Name);
        return data;
    }

    private static List<Position> Load(string file) =>
        [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", file))
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(l =>
            {
                string[] f = l.Split('|');
                Point[] moves = [.. f[3].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(m =>
                {
                    string[] xy = m.Split(',');
                    return new Point(int.Parse(xy[0], CultureInfo.InvariantCulture), int.Parse(xy[1], CultureInfo.InvariantCulture));
                })];
                return new Position(f[0], Enum.Parse<GomokuVariant>(f[1]), int.Parse(f[2], CultureInfo.InvariantCulture), moves, f[4..]);
            })];
}
