namespace Gomoku.Core.AI;

/// <summary>
/// Specifies how thinking time or depth is constrained for an AI search.
/// </summary>
public enum TimeControlMode
{
    FixedDepth,
    TimePerMove,
    TimePerGame
}

/// <summary>
/// Encapsulates search limits passed to AI players.
/// </summary>
public sealed record SearchLimits
{
    public TimeControlMode Mode { get; }
    public int Depth { get; }
    public TimeSpan Time { get; }
    public bool UseOpeningBook { get; init; }

    private SearchLimits(TimeControlMode mode, int depth, TimeSpan time, bool useOpeningBook = true)
    {
        Mode = mode;
        Depth = depth;
        Time = time;
        UseOpeningBook = useOpeningBook;
    }

    public static SearchLimits FixedDepth(int plies, bool useOpeningBook = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(plies, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(plies, 50);
        return new SearchLimits(TimeControlMode.FixedDepth, plies, TimeSpan.Zero, useOpeningBook);
    }

    public static SearchLimits TimePerMove(TimeSpan time, bool useOpeningBook = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(time, TimeSpan.Zero);
        return new SearchLimits(TimeControlMode.TimePerMove, 0, time, useOpeningBook);
    }

    public static SearchLimits TimePerGame(TimeSpan remaining, bool useOpeningBook = true) =>
        new(TimeControlMode.TimePerGame, 0, remaining, useOpeningBook);
}

