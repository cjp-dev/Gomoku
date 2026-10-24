using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.Core.Tests;

public class CarbonXAiTests
{
    [Fact]
    public async Task CarbonX_EmptyBoard_ShouldPlayCenter()
    {
        CarbonXAiPlayer ai = new(threadCount: 1);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var legal = RuleEngine.Instance.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        move.Position.Should().Be(new Point(7, 7));
    }

    [Fact]
    public async Task CarbonX_SingleThreaded_ImmediateWin_ShouldPlayWinningMove()
    {
        CarbonXAiPlayer ai = new(threadCount: 1);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // Black has 4 stones in a row: (7, 3), (7, 4), (7, 5), (7, 6)
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
        // Winning move is either (2, 7) or (7, 7)
        move.Position.Should().Match<Point>(p => p == new Point(2, 7) || p == new Point(7, 7));
    }

    [Fact]
    public async Task CarbonX_Multithreaded_ImmediateWin_ShouldPlayWinningMove()
    {
        CarbonXAiPlayer ai = new(threadCount: 4);
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

    [Fact]
    public async Task CarbonX_MustBlockOpponentFour_ShouldBlock()
    {
        CarbonXAiPlayer ai = new(threadCount: 1);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // White to move; Black has 4 stones: (3, 7), (4, 7), (5, 7), (6, 7)
        state = rules.ApplyMove(state, new Move(3, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));

        state.SideToMove.Should().Be(PieceColor.White);
        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        // White must block at either (2, 7) or (7, 7)
        move.Position.Should().Match<Point>(p => p == new Point(2, 7) || p == new Point(7, 7));
    }

    [Fact]
    public async Task CarbonX_DiagonalWin_ShouldWinImmediately()
    {
        CarbonXAiPlayer ai = new(threadCount: 1);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // Black has diagonal stones at (3, 3), (4, 4), (5, 5), (6, 6)
        state = rules.ApplyMove(state, new Move(3, 3, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 4, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 5, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 6, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        move.Position.Should().Match<Point>(p => p == new Point(2, 2) || p == new Point(7, 7));
    }

    [Fact]
    public async Task CarbonX_DiagonalBlock_WhiteMustBlockBlackDiagonalFour()
    {
        CarbonXAiPlayer ai = new(threadCount: 1);
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;

        // White to move; Black has diagonal stones at (3, 3), (4, 4), (5, 5), (6, 6)
        state = rules.ApplyMove(state, new Move(3, 3, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(4, 4, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 5, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 6, PieceColor.Black));

        state.SideToMove.Should().Be(PieceColor.White);
        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        Move move = await ai.GetMoveAsync(state, limits, legal);
        move.Position.Should().Match<Point>(p => p == new Point(2, 2) || p == new Point(7, 7));
    }
}
