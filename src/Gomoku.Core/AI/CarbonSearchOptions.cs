namespace Gomoku.Core.AI;

/// <summary>
/// Switches for search features of <see cref="CarbonBitboardAiPlayer"/>, so a feature can be
/// compared with and without in the same build (benchmarks and engine matches).
/// <see cref="Default"/> is what the game plays with.
/// </summary>
public sealed record CarbonSearchOptions
{
    public static CarbonSearchOptions Default { get; } = new();

    /// <summary>B2: principal variation search (zero-width window after the first move, re-search on fail-high).</summary>
    public bool Pvs { get; init; }

    /// <summary>
    /// B4: late move reductions. From the 4th searched move, at remaining depth 3 or more, a quiet move
    /// (no three or four for either side on that cell) is first searched 2 plies shallower with a zero-width
    /// window; only if it then looks better than the best move is it searched normally. 2 plies keep the
    /// depth parity, which Carbon's table cutoffs require.
    /// </summary>
    public bool Lmr { get; init; } = true;

    /// <summary>
    /// B6: Carbon's own candidate set (the 3x3 ring plus the cells two steps away along the four lines, with
    /// priority + 1 only next to a stone) instead of the full 5x5 square with priority + 1 everywhere.
    /// </summary>
    public bool CarbonCandidates { get; init; } = true;

    /// <summary>
    /// Parses "name=1,other=0" (names are the property names, case-insensitive); unknown names throw.
    /// </summary>
    public static CarbonSearchOptions Parse(string? text)
    {
        CarbonSearchOptions options = Default;
        if (string.IsNullOrWhiteSpace(text) || text == "default")
        {
            return options;
        }

        foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            bool on = kv.Length == 1 || kv[1] is "1" or "true" or "on";
            var property = typeof(CarbonSearchOptions).GetProperties()
                .FirstOrDefault(p => p.PropertyType == typeof(bool) && string.Equals(p.Name, kv[0], StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown search option '{kv[0]}'.");
            options = options with { };
            property.SetValue(options, on);
        }

        return options;
    }

    public override string ToString()
    {
        var flags = typeof(CarbonSearchOptions).GetProperties()
            .Where(p => p.PropertyType == typeof(bool))
            .Select(p => $"{p.Name}={((bool)p.GetValue(this)! ? 1 : 0)}");
        string text = string.Join(",", flags);
        return text.Length > 0 ? text : "default";
    }
}
