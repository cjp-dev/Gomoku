using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.Core.Tests;

public class CarbonAiMultiThreadingTests
{
    [Fact]
    public void PatternBoard_Clone_ShouldProduceIdenticalIndependentCopy()
    {
        PatternBoard original = new();
        original.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        original.MakeMove(7, 7);
        original.MakeMove(7, 8);
        original.MakeMove(8, 7);

        PatternBoard clone = original.Clone();

        clone.BoardWidth.Should().Be(original.BoardWidth);
        clone.BoardHeight.Should().Be(original.BoardHeight);
        clone.Variant.Should().Be(original.Variant);
        clone.Who.Should().Be(original.Who);
        clone.MoveCount.Should().Be(original.MoveCount);
        clone.RemCount.Should().Be(original.RemCount);
        clone.ZobristHash.Should().Be(original.ZobristHash);
        clone.ZobristKey.Should().Be(original.ZobristKey);
        clone.UpperLeftCand.Should().Be(original.UpperLeftCand);
        clone.LowerRightCand.Should().Be(original.LowerRightCand);
        clone.CheckConsistency().Should().BeTrue();

        // Mutating clone must NOT mutate original
        clone.MakeMove(6, 6);
        clone.MoveCount.Should().Be(4);
        original.MoveCount.Should().Be(3);
        clone.Cells[6 + PatternBoard.Margin, 6 + PatternBoard.Margin].Piece.Should().Be(PieceColor.White);
        original.Cells[6 + PatternBoard.Margin, 6 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Empty);

        // Mutating original must NOT mutate clone
        original.MakeMove(8, 8);
        original.MoveCount.Should().Be(4);
        clone.Cells[8 + PatternBoard.Margin, 8 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Empty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task CarbonAi_DifferentThreadCounts_ImmediateWin_ShouldSolveWinningMove(int threads)
    {
        CarbonAiPlayer ai = new(threadCount: threads);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // Black has 4 stones in a row: (3, 7), (4, 7), (5, 7), (6, 7)
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        move.Position.Should().Match<Point>(p => p == new Point(2, 7) || p == new Point(7, 7));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task CarbonAi_ImmediateBlock_ShouldBlockOpponentWin(int threads)
    {
        CarbonAiPlayer ai = new(threadCount: threads);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // Black has scattered stones, White has 4 stones in a row at (3,7)-(6,7). Black must block!
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(1, 1, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(2, 2, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(14, 14, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.White));

        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        move.Position.Should().Match<Point>(p => p == new Point(2, 7) || p == new Point(7, 7));
    }

    [Fact]
    public async Task BaselineCarbonAi_ShouldFunctionIdenticallyOnStandardTactics()
    {
        BaselineCarbonAiPlayer baseline = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await baseline.GetMoveAsync(state, limits, legal);
        move.Position.Should().Match<Point>(p => p == new Point(2, 7) || p == new Point(7, 7));
    }
}
