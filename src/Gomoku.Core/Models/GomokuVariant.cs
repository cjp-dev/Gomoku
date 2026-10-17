namespace Gomoku.Core.Models;

/// <summary>
/// Gomoku rule variants supported by the engine.
/// </summary>
public enum GomokuVariant
{
    /// <summary>
    /// Freestyle Gomoku: The first player to form 5 or more consecutive stones wins.
    /// Overlines (6+ stones) count as wins. No forbidden moves.
    /// </summary>
    Freestyle,

    /// <summary>
    /// Standard Gomoku (Exact 5): Exactly 5 stones in a row wins.
    /// Overlines (6+ stones) do not count as wins for either player.
    /// </summary>
    Exact5,

    /// <summary>
    /// Renju: Black (first player) wins only on exact 5. Black is forbidden from playing
    /// Double-Three (3x3), Double-Four (4x4), or Overlines (6+).
    /// White has no restrictions and wins on 5 or more stones.
    /// </summary>
    Renju
}

public static class GomokuVariantExtensions
{
    public static string DisplayName(this GomokuVariant variant) => variant switch
    {
        GomokuVariant.Freestyle => "Freestyle Gomoku",
        GomokuVariant.Exact5 => "Exact 5 (Standard)",
        GomokuVariant.Renju => "Renju",
        _ => variant.ToString()
    };
}

