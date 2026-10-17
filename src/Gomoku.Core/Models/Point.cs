namespace Gomoku.Core.Models;

/// <summary>
/// Represents a coordinate (X, Y) on the Gomoku board.
/// X is 0-indexed column (0 = A, 1 = B, ..., 7 = H, 8 = I, etc.).
/// Y is 0-indexed row (0 = row 1 at top, Height - 1 = bottom row).
/// </summary>
public readonly record struct Point(int X, int Y)
{
    public static Point Empty => new(-1, -1);

    public bool IsValid(int width, int height) => X >= 0 && X < width && Y >= 0 && Y < height;
    public bool IsValid(BoardSize size) => IsValid(size.Width, size.Height);

    /// <summary>
    /// Returns human-readable algebraic coordinate notation, e.g. "H8" (col 7, row 7 on 15x15).
    /// Uses consecutive letters A-T (including 'I').
    /// </summary>
    public string ToCoordinateString() => $"{(char)('A' + X)}{Y + 1}";

    /// <summary>
    /// Returns standard SGF coordinate notation, e.g. "hh" for (7, 7).
    /// </summary>
    public string ToSgfString() => $"{(char)('a' + X)}{(char)('a' + Y)}";

    public override string ToString() => ToCoordinateString();

    public static bool TryParseCoordinate(string? text, BoardSize size, out Point point)
    {
        point = Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        if (text.Length < 2 || text.Length > 3) return false;

        char colChar = char.ToUpperInvariant(text[0]);
        if (colChar < 'A' || colChar > 'Z') return false;
        int x = colChar - 'A';

        if (!int.TryParse(text.AsSpan(1), out int rowNum)) return false;
        int y = rowNum - 1;

        Point candidate = new(x, y);
        if (candidate.IsValid(size))
        {
            point = candidate;
            return true;
        }

        return false;
    }

    public static bool TryParseSgf(string? text, BoardSize size, out Point point)
    {
        point = Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length != 2) return false;

        int x = char.ToLowerInvariant(text[0]) - 'a';
        int y = char.ToLowerInvariant(text[1]) - 'a';

        Point candidate = new(x, y);
        if (candidate.IsValid(size))
        {
            point = candidate;
            return true;
        }

        return false;
    }
}

