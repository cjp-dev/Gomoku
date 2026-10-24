using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.Core.Tests;

public class CarbonBitboardAiTests
{
    [Fact]
    public void BitboardPatternBoard_MakeAndUndo_ShouldPreserveConsistencyAndHash()
    {
        BitboardPatternBoard pb = new();
        pb.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        ulong hash0 = pb.ZobristHash;
        pb.MakeMove(7, 7);
        pb.MakeMove(7, 8);
        pb.MakeMove(8, 7);

        pb.CheckConsistency().Should().BeTrue();
        pb.Occupied[7].Should().Be((1u << 7) | (1u << 8));
        pb.Occupied[8].Should().Be(1u << 7);

        pb.UndoMove();
        pb.UndoMove();
        pb.UndoMove();

        pb.CheckConsistency().Should().BeTrue();
        pb.ZobristHash.Should().Be(hash0);
        pb.Occupied[7].Should().Be(0);
        pb.Occupied[8].Should().Be(0);
    }

    [Fact]
    public void BitboardPatternBoard_Clone_ShouldProduceIndependentCopy()
    {
        BitboardPatternBoard original = new();
        original.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);
        original.MakeMove(7, 7);
        original.MakeMove(7, 8);

        BitboardPatternBoard clone = original.Clone();
        clone.CheckConsistency().Should().BeTrue();
        clone.MoveCount.Should().Be(original.MoveCount);

        clone.MakeMove(8, 8);
        clone.MoveCount.Should().Be(3);
        original.MoveCount.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task CarbonBitboardAi_ImmediateWin_ShouldPlayWinningMove(int threads)
    {
        CarbonBitboardAiPlayer ai = new(threadCount: threads);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // Black has 4 stones in a row
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
    public async Task CarbonBitboardAi_ImmediateBlock_ShouldBlockOpponentWin(int threads)
    {
        CarbonBitboardAiPlayer ai = new(threadCount: threads);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // White has 4 stones in a row, Black must block
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
    public void BitboardOpeningBook_ShouldDirectlyFindMoves()
    {
        PatternBoard pbLegacy = new();
        pbLegacy.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);
        pbLegacy.MakeMove(7, 7);

        BitboardPatternBoard pbBitboard = new();
        pbBitboard.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);
        pbBitboard.MakeMove(7, 7);

        bool legacyFound = OpeningBook.TryGetMove(pbLegacy, out Point legacyMove);
        bool bitboardFound = OpeningBook.TryGetMove(pbBitboard, out Point bitboardMove);

        legacyFound.Should().BeTrue();
        bitboardFound.Should().BeTrue();
        bitboardMove.X.Should().BeInRange(0, 14);
        bitboardMove.Y.Should().BeInRange(0, 14);
    }

    [Theory]
    [InlineData(GomokuVariant.Freestyle)]
    [InlineData(GomokuVariant.Exact5)]
    [InlineData(GomokuVariant.Renju)]
    public async Task CarbonBitboardAi_Variants_ShouldPlayLegalMoves(GomokuVariant variant)
    {
        CarbonBitboardAiPlayer ai = new(threadCount: 2);
        BoardState state = new(BoardSize.Size15x15, variant);
        var rules = RuleEngine.Instance;

        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: true);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        legal.Should().Contain(move);
    }
}

