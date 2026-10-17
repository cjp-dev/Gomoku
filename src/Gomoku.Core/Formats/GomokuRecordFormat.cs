using System.Text;
using System.Text.RegularExpressions;
using Gomoku.Core.Models;

namespace Gomoku.Core.Formats;

/// <summary>
/// Parser and serializer for human-readable Gomoku coordinate records with bracketed metadata tags.
/// </summary>
public static partial class GomokuRecordFormat
{
    [GeneratedRegex(@"^\[(?<key>\w+)\s+""(?<value>[^""]*)""\]$")]
    private static partial Regex TagRegex();

    public static string Serialize(
        IReadOnlyList<Move> moves,
        BoardSize size,
        GomokuVariant variant = GomokuVariant.Freestyle,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        StringBuilder sb = new();
        sb.AppendLine("[Game \"Gomoku\"]");
        sb.AppendLine($"[Size \"{size}\"]");
        sb.AppendLine($"[Variant \"{variant}\"]");

        if (metadata != null)
        {
            foreach (var (k, v) in metadata)
            {
                if (k is "Game" or "Size" or "Variant") continue;
                sb.AppendLine($"[{k} \"{v}\"]");
            }
        }

        sb.AppendLine();

        // Write moves numbered: 1. H8 H9 2. I9 J10 ...
        for (int i = 0; i < moves.Count; i++)
        {
            if (i % 2 == 0)
            {
                if (i > 0 && i % 10 == 0) sb.AppendLine();
                sb.Append($"{i / 2 + 1}. ");
            }
            sb.Append($"{moves[i].Position.ToCoordinateString()} ");
        }

        return sb.ToString().TrimEnd();
    }

    public static bool TryParse(
        string recordText,
        out IReadOnlyList<Move> moves,
        out BoardSize size,
        out GomokuVariant variant,
        out Dictionary<string, string> metadata)
    {
        moves = Array.Empty<Move>();
        size = BoardSize.Default;
        variant = GomokuVariant.Freestyle;
        metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(recordText)) return false;

        List<Move> parsedMoves = new();
        PieceColor turn = PieceColor.Black;

        using StringReader reader = new(recordText);
        string? line;
        bool inMoves = false;

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                inMoves = true;
                continue;
            }

            Match match = TagRegex().Match(trimmed);
            if (!inMoves && match.Success)
            {
                string key = match.Groups["key"].Value;
                string val = match.Groups["value"].Value;
                metadata[key] = val;

                if (key.Equals("Size", StringComparison.OrdinalIgnoreCase))
                {
                    if (BoardSize.TryParse(val, out BoardSize parsedSize))
                    {
                        size = parsedSize;
                    }
                }
                else if (key.Equals("Variant", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse(val, true, out GomokuVariant parsedVariant))
                    {
                        variant = parsedVariant;
                    }
                }
                continue;
            }

            // Parsing moves
            inMoves = true;
            // Tokenize by spaces
            string[] tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                // Skip move numbers like "1." or "23."
                if (token.EndsWith('.') && int.TryParse(token[..^1], out _))
                {
                    continue;
                }

                if (Point.TryParseCoordinate(token, size, out Point pt))
                {
                    parsedMoves.Add(new Move(pt.X, pt.Y, turn));
                    turn = turn.Opponent();
                }
            }
        }

        moves = parsedMoves;
        return true;
    }
}

