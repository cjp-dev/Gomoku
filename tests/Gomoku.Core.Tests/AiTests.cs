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
    public async Task CarbonAiPlayer_OpenFourCreation_ShouldReportMateIn3()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        // White to move with an open 3: (5, 7), (6, 7), (7, 7)
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(14, 0, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(0, 14, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.White));
        state = rules.ApplyMove(state, new Move(14, 14, PieceColor.Black));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        Point pt = chosen.Position;
        (pt == new Point(4, 7) || pt == new Point(8, 7)).Should().BeTrue();
        ai.LastAnalysis.Should().NotBeNull();
        ai.LastAnalysis!.Score.Should().Be(CarbonAiPlayer.WinMax - 3);
        ai.LastAnalysis.Nodes.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CarbonAiPlayer_DiagonalOpenFour_ShouldReportMateIn3()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        RuleEngine rules = RuleEngine.Instance;
        // White to move with diagonal open 3: (4, 4), (5, 5), (6, 6)
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(4, 4, PieceColor.White));
        state = rules.ApplyMove(state, new Move(14, 0, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(5, 5, PieceColor.White));
        state = rules.ApplyMove(state, new Move(0, 14, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(6, 6, PieceColor.White));
        state = rules.ApplyMove(state, new Move(14, 14, PieceColor.Black));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        Point pt = chosen.Position;
        (pt == new Point(3, 3) || pt == new Point(7, 7)).Should().BeTrue();
        ai.LastAnalysis.Should().NotBeNull();
        ai.LastAnalysis!.Score.Should().Be(CarbonAiPlayer.WinMax - 3);
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

    [Fact]
    public void PatternBoard_IncrementalZobristKey_ShouldMatchBoardStateHashAcrossMakeAndUndo()
    {
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        PatternBoard pb = new();
        pb.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        Point[] moves = [new(7, 7), new(7, 8), new(8, 8), new(6, 6), new(8, 7)];
        foreach (Point pt in moves)
        {
            state = RuleEngine.Instance.ApplyMove(state, new Move(pt.X, pt.Y, state.SideToMove));
            pb.MakeMove(pt.X, pt.Y);
            pb.ZobristKey.Should().Be(Zobrist.ComputeHash(state));
        }

        // Undo two moves and verify ZobristKey reverts identically
        pb.UndoMove();
        pb.UndoMove();

        BoardState state3 = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        for (int i = 0; i < 3; i++)
        {
            state3 = RuleEngine.Instance.ApplyMove(state3, new Move(moves[i].X, moves[i].Y, state3.SideToMove));
        }

        pb.ZobristKey.Should().Be(Zobrist.ComputeHash(state3));
    }

    [Fact]
    public async Task CarbonAiPlayer_RenjuMode_ShouldNotPlayForbiddenDoubleThree()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Renju);
        RuleEngine rules = RuleEngine.Instance;

        // Construct a position where (7, 7) is a double-three (3x3) foul for Black
        state = rules.ApplyMove(state, new Move(7, 5, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 0, PieceColor.White));
        state = rules.ApplyMove(state, new Move(7, 6, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 1, PieceColor.White));
        state = rules.ApplyMove(state, new Move(5, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 2, PieceColor.White));
        state = rules.ApplyMove(state, new Move(6, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(0, 3, PieceColor.White));

        var legalMoves = rules.GetLegalMoves(state);
        legalMoves.Should().NotContain(new Move(7, 7, PieceColor.Black));

        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);
        Move chosen = await ai.GetMoveAsync(state, limits, legalMoves);

        chosen.Position.Should().NotBe(new Point(7, 7));
        rules.IsMoveLegal(state, chosen, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CarbonAiPlayer_WithProgress_ShouldEmitSearchAnalysisUpdates()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        RuleEngine rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        state = rules.ApplyMove(state, new Move(7, 8, PieceColor.White));
        state = rules.ApplyMove(state, new Move(8, 7, PieceColor.Black));

        var legalMoves = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: false);

        List<SearchAnalysis> reports = [];
        var progress = new SyncProgress<SearchAnalysis>(reports.Add);

        Move best = await ai.GetMoveAsync(state, limits, legalMoves, progress);
        best.Position.Should().NotBe(Point.Empty);

        reports.Should().NotBeEmpty();
        reports.Any(r => r.Depth >= 2 && !r.IsSearching).Should().BeTrue();
        reports.Last().IsSearching.Should().BeFalse();
        reports.Last().Depth.Should().Be(4);
    }

    [Fact]
    public async Task CarbonAiPlayer_CenterOpening_ShouldReportCenterOpeningStatus()
    {
        CarbonAiPlayer ai = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var legalMoves = RuleEngine.Instance.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(4, useOpeningBook: true);

        List<SearchAnalysis> reports = [];
        var progress = new SyncProgress<SearchAnalysis>(reports.Add);

        Move centerMove = await ai.GetMoveAsync(state, limits, legalMoves, progress);
        centerMove.Position.Should().Be(new Point(7, 7));

        reports.Should().HaveCount(1);
        reports[0].StatusMessage.Should().Be("Center opening");
        reports[0].IsSearching.Should().BeFalse();
    }

    [Fact]
    public async Task AiPlayers_MoveNowToken_ShouldReturnLegalMoveWithoutThrowing()
    {
        CarbonAiPlayer carbonAi = new();
        SimpleAiPlayer simpleAi = new();

        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        state = RuleEngine.Instance.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        var legal = RuleEngine.Instance.GetLegalMoves(state);

        SearchLimits limits = SearchLimits.FixedDepth(10, useOpeningBook: false);

        // Pre-cancelled moveNowToken
        using var moveNowCts = new CancellationTokenSource();
        moveNowCts.Cancel();

        Move carbonMove = await carbonAi.GetMoveAsync(state, limits, legal, progress: null, cancellationToken: CancellationToken.None, moveNowToken: moveNowCts.Token);
        legal.Should().Contain(carbonMove);

        Move simpleMove = await simpleAi.GetMoveAsync(state, limits, legal, progress: null, cancellationToken: CancellationToken.None, moveNowToken: moveNowCts.Token);
        legal.Should().Contain(simpleMove);
    }

    [Fact]
    public async Task AiPlayers_CancellationToken_ShouldThrowOperationCanceledException()
    {
        CarbonAiPlayer carbonAi = new();
        SimpleAiPlayer simpleAi = new();

        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        state = RuleEngine.Instance.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        var legal = RuleEngine.Instance.GetLegalMoves(state);

        SearchLimits limits = SearchLimits.FixedDepth(10, useOpeningBook: false);

        using var cancelCts = new CancellationTokenSource();
        cancelCts.Cancel();

        Func<Task> carbonAct = async () => await carbonAi.GetMoveAsync(state, limits, legal, progress: null, cancellationToken: cancelCts.Token, moveNowToken: CancellationToken.None);
        await carbonAct.Should().ThrowAsync<OperationCanceledException>();

        Func<Task> simpleAct = async () => await simpleAi.GetMoveAsync(state, limits, legal, progress: null, cancellationToken: cancelCts.Token, moveNowToken: CancellationToken.None);
        await simpleAct.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}

