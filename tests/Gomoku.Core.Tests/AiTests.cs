using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.Tests;

public class AiTests
{
    [Fact]
    public async Task AiPlayers_EmptyBoard_ShouldPlayCenter()
    {
        CarbonAiPlayer carbonAi = new();
        SimpleAiPlayer simpleAi = new();

        BoardState state15 = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var legal15 = RuleEngine.Instance.GetLegalMoves(state15);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: true);

        Move carbonMove15 = await carbonAi.GetMoveAsync(state15, limits, legal15);
        carbonMove15.Position.Should().Be(new Point(7, 7));

        Move simpleMove15 = await simpleAi.GetMoveAsync(state15, limits, legal15);
        simpleMove15.Position.Should().Be(new Point(7, 7));

        BoardState state19 = new(BoardSize.Size19x19, GomokuVariant.Freestyle);
        var legal19 = RuleEngine.Instance.GetLegalMoves(state19);
        Move carbonMove19 = await carbonAi.GetMoveAsync(state19, limits, legal19);
        carbonMove19.Position.Should().Be(new Point(9, 9));
    }

    [Fact]
    public void OpeningBook_ResponseToCenterMove_ShouldReturnValidMove()
    {
        PatternBoard pb = new();
        pb.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);
        pb.MakeMove(7, 7); // Move 1: Black plays center H8

        bool found = OpeningBook.TryGetMove(pb, out Point response);
        found.Should().BeTrue();
        response.IsValid(15, 15).Should().BeTrue();
        // Response should be next to center, e.g. within distance 2
        Math.Abs(response.X - 7).Should().BeLessThanOrEqualTo(2);
        Math.Abs(response.Y - 7).Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task SimpleAiPlayer_ImmediateWin_ShouldPlayWinningMove()
    {
        SimpleAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        Point pt = chosen.Position;
        (pt == new Point(2, 7) || pt == new Point(7, 7)).Should().BeTrue();
    }

    [Fact]
    public async Task CarbonAiPlayer_ImmediateWin_ShouldPlayWinningMove()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        Point pt = chosen.Position;
        (pt == new Point(2, 7) || pt == new Point(7, 7)).Should().BeTrue();
    }

    [Fact]
    public async Task CarbonAiPlayer_ShouldBlockOpponentImmediateThreat()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        Point pt = chosen.Position;
        (pt == new Point(2, 7) || pt == new Point(7, 7)).Should().BeTrue();
    }

    [Fact]
    public async Task CarbonAiPlayer_TranspositionTable_ShouldAccumulateEntries()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(7, 8, PieceColor.White));
        state = rules.ApplyMove(state, new Move(8, 7, PieceColor.Black));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move best = await ai.GetMoveAsync(state, limits, legalMoves);
        best.Position.Should().NotBe(Point.Empty);

        ai.Table.Stores.Should().BeGreaterThan(0);
    }
}

