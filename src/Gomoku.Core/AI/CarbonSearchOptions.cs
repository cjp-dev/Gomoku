namespace Gomoku.Core.AI;

/// <summary>
/// Switches for search features of <see cref="CarbonBitboardAiPlayer"/>, so a feature can be
/// compared with and without in the same build (benchmarks and engine matches).
/// <see cref="Default"/> is what the game plays with.
/// </summary>
public sealed record CarbonSearchOptions
{
    public static CarbonSearchOptions Default { get; } = new();

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
