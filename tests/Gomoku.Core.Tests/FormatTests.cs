using FluentAssertions;
using Gomoku.Core.Formats;
using Gomoku.Core.Models;

namespace Gomoku.Core.Tests;

public class FormatTests
{
    [Fact]
    public void SgfFormat_RoundTrip_ShouldPreserveGameData()
    {
        BoardSize size = BoardSize.Size15x15;
        List<Move> moves = [
            new Move(7, 7, PieceColor.Black),
            new Move(7, 8, PieceColor.White),
            new Move(8, 7, PieceColor.Black)
        ];

        string sgf = SgfFormat.Serialize(
            moves,
            size,
            GomokuVariant.Freestyle,
            blackPlayer: "Alice",
            whitePlayer: "Bob"
        );

        sgf.Should().Contain("SZ[15]");
        sgf.Should().Contain("RU[Freestyle]");
        sgf.Should().Contain("PB[Alice]");
        sgf.Should().Contain("PW[Bob]");

        bool success = SgfFormat.TryParse(sgf, out var parsedMoves, out var parsedSize, out var parsedVariant, out var meta);
        success.Should().BeTrue();
        parsedMoves.Should().HaveCount(3);
        parsedMoves[0].Should().Be(new Move(7, 7, PieceColor.Black));
        parsedMoves[1].Should().Be(new Move(7, 8, PieceColor.White));
        parsedMoves[2].Should().Be(new Move(8, 7, PieceColor.Black));
        meta["PB"].Should().Be("Alice");
        meta["PW"].Should().Be("Bob");
        parsedSize.Width.Should().Be(15);
        parsedVariant.Should().Be(GomokuVariant.Freestyle);
    }

    [Fact]
    public void GomokuRecordFormat_RoundTrip_ShouldPreserveCoordinates()
    {
        BoardSize size = BoardSize.Size15x15;
        List<Move> moves = [
            new Move(7, 7, PieceColor.Black), // H8
            new Move(8, 7, PieceColor.White), // I8
            new Move(7, 8, PieceColor.Black)  // H9
        ];

        var metadata = new Dictionary<string, string>
        {
            ["Event"] = "Championship",
            ["Black"] = "Player 1",
            ["White"] = "Player 2"
        };

        string text = GomokuRecordFormat.Serialize(
            moves,
            size,
            GomokuVariant.Renju,
            metadata
        );

        text.Should().Contain("[Variant \"Renju\"]");
        text.Should().Contain("1. H8 I8");
        text.Should().Contain("2. H9");

        bool success = GomokuRecordFormat.TryParse(text, out var parsedMoves, out var parsedSize, out var parsedVariant, out var parsedMeta);
        success.Should().BeTrue();
        parsedMoves.Should().HaveCount(3);
        parsedMoves[0].X.Should().Be(7);
        parsedMoves[0].Y.Should().Be(7);
        parsedMoves[1].X.Should().Be(8);
        parsedMoves[1].Y.Should().Be(7);
        parsedVariant.Should().Be(GomokuVariant.Renju);
        parsedMeta["Event"].Should().Be("Championship");
    }

    [Fact]
    public void GameFileSerializer_AutoDetect_ShouldWorkForBothFormats()
    {
        string sgfSample = "(;GM[11]FF[4]SZ[15]RU[Freestyle]PB[Black]PW[White];B[hh];W[hi])";
        bool sgfOk = GameFileSerializer.TryParse(sgfSample, out var movesSgf, out var sizeSgf, out var varSgf, out var metaSgf);
        sgfOk.Should().BeTrue();
        sizeSgf.Width.Should().Be(15);
        movesSgf.Should().HaveCount(2);

        string gomokuSample = "[Size \"19x19\"]\n[Variant \"Freestyle\"]\n[Black \"BPlayer\"]\n[White \"WPlayer\"]\n\n1. J10 K10";
        bool gOk = GameFileSerializer.TryParse(gomokuSample, out var movesG, out var sizeG, out var varG, out var metaG);
        gOk.Should().BeTrue();
        sizeG.Width.Should().Be(19);
        movesG.Should().HaveCount(2);
    }
}

