using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Symmetry-aware opening book ported directly from Carbon Gomoku's databaseMove.
/// </summary>
public static class OpeningBook
{
    private static readonly Random Rng = new();

    public static bool TryGetMove(PatternBoard pb, out Point move)
    {
        move = Point.Empty;
        if (pb.MoveCount < 1) return false;

        ReadOnlySpan<sbyte> data = CarbonTables.OpeningBookData;
        int ptr = 0;

        int left = pb.UpperLeftCand.X + 2;
        int top = pb.UpperLeftCand.Y + 2;
        int right = pb.LowerRightCand.X - 2;
        int bottom = pb.LowerRightCand.Y - 2;

        while (ptr < data.Length - 1)
        {
            int len1 = data[ptr++];
            int len2 = data[ptr++];
            if (len1 == 0 && len2 == 0) break;

            int nextRecord = ptr + 2 * (len1 + len2);
            if (len1 != pb.MoveCount)
            {
                if (len1 < pb.MoveCount) return false; // Data is sorted by moveCount descending
                ptr = nextRecord;
                continue;
            }

            // Try all 8 dihedral symmetries (4 rotations x 2 reflections)
            for (int flip = 0; flip < 8; flip++)
            {
                int sBase = ptr;
                for (int i = 0; ; i++)
                {
                    int x1, y1;
                    if (i == len1)
                    {
                        int choice = Rng.Next(len2);
                        int candPtr = sBase + 2 * (len1 + choice);
                        x1 = data[candPtr];
                        y1 = data[candPtr + 1];
                    }
                    else
                    {
                        x1 = data[sBase + 2 * i];
                        y1 = data[sBase + 2 * i + 1];
                    }

                    int x, y;
                    switch (flip)
                    {
                        case 0: x = left + x1; y = top + y1; break;
                        case 1: x = right - x1; y = top + y1; break;
                        case 2: x = left + x1; y = bottom - y1; break;
                        case 3: x = right - x1; y = bottom - y1; break;
                        case 4: x = left + y1; y = top + x1; break;
                        case 5: x = right - y1; y = top + x1; break;
                        case 6: x = left + y1; y = bottom - x1; break;
                        default: x = right - y1; y = bottom - x1; break;
                    }

                    // Keep margin from border
                    const int borderMargin = 4;
                    int boardX = x - PatternBoard.Margin;
                    int boardY = y - PatternBoard.Margin;
                    if (boardX < borderMargin || boardX >= pb.BoardWidth - borderMargin ||
                        boardY < borderMargin || boardY >= pb.BoardHeight - borderMargin)
                    {
                        break;
                    }

                    if (i == len1)
                    {
                        // Found a matching book move! Check that it's empty
                        if (pb.Cells[x, y].Piece == PieceColor.Empty)
                        {
                            move = new Point(boardX, boardY);
                            return true;
                        }
                        break;
                    }

                    // Compare placed stone color: Black is FirstPlayer (i & 1 == 0), White is SecondPlayer (i & 1 == 1)
                    PieceColor expectedPiece = (i & 1) == 0 ? PieceColor.Black : PieceColor.White;
                    if (pb.Cells[x, y].Piece != expectedPiece)
                    {
                        break;
                    }
                }
            }

            ptr = nextRecord;
        }

        return false;
    }
}

