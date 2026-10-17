using FluentAssertions;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.Tests;

public class PatternAndTablesTests
{
    [Fact]
    public void CarbonTables_DimensionsAndValues_ShouldBeValid()
    {
        CarbonTables.ConfigFlat.Length.Should().Be(65536);
        CarbonTables.PriorFlat.Length.Should().Be(65536);
        CarbonTables.Count5Flat.Length.Should().Be(107 * 5);
        CarbonTables.Status1StandardFlat.Length.Should().Be(65536);
        CarbonTables.Status1Exact5Flat.Length.Should().Be(65536);
        CarbonTables.Rank.Length.Should().Be(107);
        CarbonTables.Status4.Length.Should().Be(10 * 10 * 10 * 10);
        CarbonTables.OpeningBookData.Length.Should().BeGreaterThan(100);

        // Sanity checks on specific table entries
        // Config: 0 (empty line of 8 cells) -> 1
        CarbonTables.GetConfig(0, 0).Should().Be(1);

        // Rank bounds
        CarbonTables.Rank[0].Should().Be(0);
        CarbonTables.Rank[106].Should().BeGreaterThan(CarbonTables.Rank[1]);

        // Status4 index: 0, 0, 0, 0 -> 0
        CarbonTables.GetStatus4(0, 0, 0, 0).Should().Be(0);
    }

    [Fact]
    public void PatternBoard_PlaceAndUndoMove_ShouldMaintainConsistency()
    {
        PatternBoard board = new();
        board.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        board.CheckConsistency().Should().BeTrue();

        // Place moves
        board.MakeMove(7, 7); // Black
        board.CheckConsistency().Should().BeTrue();
        board.Cells[7 + PatternBoard.Margin, 7 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Black);

        board.MakeMove(7, 8); // White
        board.CheckConsistency().Should().BeTrue();
        board.Cells[7 + PatternBoard.Margin, 8 + PatternBoard.Margin].Piece.Should().Be(PieceColor.White);

        board.MakeMove(8, 7); // Black
        board.CheckConsistency().Should().BeTrue();

        // Undo
        board.UndoMove();
        board.CheckConsistency().Should().BeTrue();
        board.Cells[8 + PatternBoard.Margin, 7 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Empty);

        board.UndoMove();
        board.CheckConsistency().Should().BeTrue();
        board.Cells[7 + PatternBoard.Margin, 8 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Empty);

        board.UndoMove();
        board.CheckConsistency().Should().BeTrue();
        board.Cells[7 + PatternBoard.Margin, 7 + PatternBoard.Margin].Piece.Should().Be(PieceColor.Empty);
    }

    [Fact]
    public void PatternBoard_AdjacencyAndBoundingBox_ShouldUpdateCorrectly()
    {
        PatternBoard board = new();
        board.Start(15, 15, GomokuVariant.Freestyle, PieceColor.Black);

        board.MakeMove(7, 7); // Black at (7, 7)

        // Cell (7, 8) in board coordinates is adjacent
        int xp = 7 + PatternBoard.Margin;
        int yp = 8 + PatternBoard.Margin;
        board.Cells[xp, yp].Adj1.Should().BeGreaterThan((sbyte)0);

        // Bounding box in padded coordinates
        board.UpperLeftCand.X.Should().BeLessThanOrEqualTo(xp);
        board.UpperLeftCand.Y.Should().BeLessThanOrEqualTo(yp);
        board.LowerRightCand.X.Should().BeGreaterThanOrEqualTo(xp);
        board.LowerRightCand.Y.Should().BeGreaterThanOrEqualTo(yp);
    }
}

