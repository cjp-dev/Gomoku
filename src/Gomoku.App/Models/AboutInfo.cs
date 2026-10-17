namespace Gomoku.App.Models;

/// <summary>
/// Information displayed in the About dialog.
/// </summary>
public sealed record AboutInfo
{
    public string Title { get; init; } = "Gomoku .NET";
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "Claus Pedersen";
    public string Year { get; init; } = "2026";
    public string Description { get; init; } =
        "A high-performance, cross-platform Gomoku (Five in a Row) application in C# and .NET 10. " +
        "Engine inspired by Michał Czardybon's world-class Carbon Gomoku, featuring bitboard pattern recognition, " +
        "VCF tactical search, Zobrist transposition tables, and Clean Architecture.";
    public string RepositoryUrl { get; init; } = "https://github.com/clauspedersen/gomoku";
}

