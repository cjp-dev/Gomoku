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

    /// <summary>LMR: the first searched move that may be reduced (0-based).</summary>
    public int LmrFromMove { get; init; } = 3;

    /// <summary>LMR: the smallest remaining depth at which moves are reduced.</summary>
    public int LmrMinDepth { get; init; } = 3;

    /// <summary>LMR: from this searched move on, reduce 4 plies instead of 2 (at remaining depth 5 or more); 0 = never.</summary>
    public int LmrDeepFromMove { get; init; }

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
            var property = typeof(CarbonSearchOptions).GetProperties()
                .FirstOrDefault(p => (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int)) && string.Equals(p.Name, kv[0], StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown search option '{kv[0]}'.");
            options = options with { };
            if (property.PropertyType == typeof(int))
            {
                property.SetValue(options, int.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                property.SetValue(options, kv.Length == 1 || kv[1] is "1" or "true" or "on");
            }
        }

        return options;
    }

    public override string ToString()
    {
        var flags = typeof(CarbonSearchOptions).GetProperties()
            .Where(p => p.PropertyType == typeof(bool) || p.PropertyType == typeof(int))
            .Select(p => p.GetValue(this) is bool b ? $"{p.Name}={(b ? 1 : 0)}" : $"{p.Name}={p.GetValue(this)}");
        string text = string.Join(",", flags);
        return text.Length > 0 ? text : "default";
    }
}
