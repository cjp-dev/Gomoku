namespace Gomoku.Core.Models;

/// <summary>
/// Dimensions of a Gomoku board.
/// </summary>
public readonly record struct BoardSize(int Width, int Height)
{
    public static BoardSize Size15x15 => new(15, 15);
    public static BoardSize Size19x19 => new(19, 19);
    public static BoardSize Size20x20 => new(20, 20);

    public static BoardSize Default => Size15x15;

    public int TotalCells => Width * Height;

    /// <summary>
    /// Geometric center of the board (tengen), e.g. (7, 7) on 15x15, (9, 9) on 19x19, (10, 10) on 20x20.
    /// </summary>
    public Point Center => new(Width / 2, Height / 2);

    public override string ToString() => $"{Width}x{Height}";

    /// <summary>
    /// Returns the traditional star points (hoshi) for the board dimensions.
    /// </summary>
    public IReadOnlyList<Point> GetStarPoints() => (Width, Height) switch
    {
        (15, 15) => [new(3, 3), new(11, 3), new(7, 7), new(3, 11), new(11, 11)],
        (19, 19) => [
            new(3, 3), new(9, 3), new(15, 3),
            new(3, 9), new(9, 9), new(15, 9),
            new(3, 15), new(9, 15), new(15, 15)
        ],
        (20, 20) => [new(3, 3), new(16, 3), new(3, 16), new(16, 16)],
        _ => []
    };

    public static bool TryParse(string? text, out BoardSize size)
    {
        size = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] parts = text.Trim().ToLowerInvariant().Split(['x', '*', ' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out int dim) && dim >= 5 && dim <= 64)
        {
            size = new BoardSize(dim, dim);
            return true;
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int w) && w >= 5 && w <= 64 &&
            int.TryParse(parts[1], out int h) && h >= 5 && h <= 32)
        {
            size = new BoardSize(w, h);
            return true;
        }

        return false;
    }
}

