using FluentAssertions;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.Tests;

public class BoardAndRulesTests
{
    [Theory]
    [InlineData(15, 15, "H8", 7, 7, 5)]
    [InlineData(19, 19, "J10", 9, 9, 9)]
    [InlineData(20, 20, "K11", 10, 10, 4)]
    public void BoardSize_CenterAndStarPoints_ShouldBeCorrect(int w, int h, string expectedCoord, int cx, int cy, int starCount)
    {
        BoardSize size = new(w, h);
        size.Center.X.Should().Be(cx);
        size.Center.Y.Should().Be(cy);
        size.Center.ToCoordinateString().Should().Be(expectedCoord);
        size.GetStarPoints().Count.Should().Be(starCount);

        Point.TryParseCoordinate(expectedCoord, size, out Point pt).Should().BeTrue();
        pt.Should().Be(new Point(cx, cy));
    }

    [Fact]
    public void HorizontalWin_Freestyle_ShouldTriggerBlackWon()
    {
        RuleEngine rules = RuleEngine.Instance;
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        // Black plays H8, H9, H10, H11, H12 (horizontal)
        // White plays A1, A2, A3, A4
        (int x, int y, PieceColor p)[] moves = [
            (7, 7, PieceColor.Black), (0, 0, PieceColor.White),
            (8, 7, PieceColor.Black), (0, 1, PieceColor.White),
            (9, 7, PieceColor.Black), (0, 2, PieceColor.White),
            (10, 7, PieceColor.Black), (0, 3, PieceColor.White),
            (11, 7, PieceColor.Black) // 5 in a row!
        ];

        foreach (var (x, y, p) in moves)
        {
            state = rules.ApplyMove(state, new Move(x, y, p));
        }

        state.Status.Should().Be(GameStatus.BlackWon);
        state.WinningLine.Should().HaveCount(5);
    }

    [Fact]
    public void DiagonalWin_Freestyle_ShouldTriggerWhiteWon()
    {
        RuleEngine rules = RuleEngine.Instance;
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);

        (int x, int y, PieceColor p)[] moves = [
            (0, 0, PieceColor.Black), (1, 1, PieceColor.White),
            (2, 0, PieceColor.Black), (2, 2, PieceColor.White),
            (4, 0, PieceColor.Black), (3, 3, PieceColor.White),
            (6, 0, PieceColor.Black), (4, 4, PieceColor.White),
            (8, 0, PieceColor.Black), (5, 5, PieceColor.White) // 5 in a row diagonal for White!
        ];

        foreach (var (x, y, p) in moves)
        {
            state = rules.ApplyMove(state, new Move(x, y, p));
        }

        state.Status.Should().Be(GameStatus.WhiteWon);
        state.WinningLine.Should().HaveCount(5);
    }

    [Fact]
    public void Overline_InFreestyle_Wins_InExact5_DoesNotWin()
    {
        RuleEngine rules = RuleEngine.Instance;

        // Freestyle test: 6 in a row wins
        BoardState freestyleState = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        (int x, int y, PieceColor p)[] moves = [
            (0, 0, PieceColor.Black), (5, 5, PieceColor.White),
            (1, 0, PieceColor.Black), (6, 7, PieceColor.White),
            (2, 0, PieceColor.Black), (7, 5, PieceColor.White),
            (3, 0, PieceColor.Black), (8, 7, PieceColor.White),
            (5, 0, PieceColor.Black), (9, 5, PieceColor.White), // Setup 5 stones with gap at (4,0)
        ];
        foreach (var (x, y, p) in moves)
        {
            freestyleState = rules.ApplyMove(freestyleState, new Move(x, y, p));
        }

        // Now place (4,0) to connect all 6 stones: (0,0)..(5,0)
        BoardState winFreestyle = rules.ApplyMove(freestyleState, new Move(4, 0, PieceColor.Black));
        winFreestyle.Status.Should().Be(GameStatus.BlackWon);
        winFreestyle.WinningLine.Should().HaveCount(6);

        // Exact5 test: 6 in a row does NOT win
        BoardState exact5State = new(BoardSize.Size15x15, GomokuVariant.Exact5);
        foreach (var (x, y, p) in moves)
        {
            exact5State = rules.ApplyMove(exact5State, new Move(x, y, p));
        }

        BoardState noWinExact5 = rules.ApplyMove(exact5State, new Move(4, 0, PieceColor.Black));
        noWinExact5.Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public void Renju_BlackFoul_ShouldBeRejectedAsIllegal()
    {
        RuleEngine rules = RuleEngine.Instance;
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Renju);

        // Setup 3x3 foul opportunity for Black at (7, 7):
        // Horizontal: Black at (5, 7), (6, 7)
        // Vertical: Black at (7, 5), (7, 6)
        state.SetCell(5, 7, PieceColor.Black);
        state.SetCell(6, 7, PieceColor.Black);
        state.SetCell(7, 5, PieceColor.Black);
        state.SetCell(7, 6, PieceColor.Black);
        state.MoveCount = 4;
        state.SideToMove = PieceColor.Black;

        // Playing (7, 7) creates a double-three (3x3) foul for Black
        bool isLegal = rules.IsMoveLegal(state, new Move(7, 7, PieceColor.Black), out string? reason);
        isLegal.Should().BeFalse();
        reason.Should().Contain("forbidden foul in Renju");

        // But for White, the exact same position is completely legal!
        state.SideToMove = PieceColor.White;
        bool isWhiteLegal = rules.IsMoveLegal(state, new Move(7, 7, PieceColor.White), out _);
        isWhiteLegal.Should().BeTrue();
    }
}

