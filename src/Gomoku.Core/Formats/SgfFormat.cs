using System.Text;
using Gomoku.Core.Models;

namespace Gomoku.Core.Formats;

/// <summary>
/// Parser and serializer for Smart Game Format (SGF) files (GM[11] = Gomoku).
/// </summary>
public static class SgfFormat
{
    public static string Serialize(
        IReadOnlyList<Move> moves,
        BoardSize size,
        GomokuVariant variant = GomokuVariant.Freestyle,
        string blackPlayer = "Human",
        string whitePlayer = "Carbon AI")
    {
        StringBuilder sb = new();
        sb.Append("(;GM[11]FF[4]CA[UTF-8]AP[Gomoku.Net:1.0]");
        sb.Append($"SZ[{size.Width}]");
        sb.Append($"RU[{variant}]");
        sb.Append($"PB[{Escape(blackPlayer)}]");
        sb.Append($"PW[{Escape(whitePlayer)}]");
        sb.Append("KM[0]");

        foreach (Move m in moves)
        {
            char playerTag = m.Player == PieceColor.Black ? 'B' : 'W';
            sb.Append($";{playerTag}[{m.Position.ToSgfString()}]");
        }

        sb.Append(')');
        return sb.ToString();
    }

    public static bool TryParse(
        string sgfText,
        out IReadOnlyList<Move> moves,
        out BoardSize size,
        out GomokuVariant variant,
        out Dictionary<string, string> properties)
    {
        moves = Array.Empty<Move>();
        size = BoardSize.Default;
        variant = GomokuVariant.Freestyle;
        properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sgfText)) return false;

        string text = sgfText.Trim();
        if (!text.StartsWith('(') || !text.EndsWith(')')) return false;

        // Parse key-value properties
        int i = 0;
        List<Move> parsedMoves = new();
        PieceColor expectedPlayer = PieceColor.Black;

        while (i < text.Length)
        {
            if (char.IsLetter(text[i]))
            {
                int propStart = i;
                while (i < text.Length && char.IsLetter(text[i])) i++;
                string propName = text[propStart..i];

                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

                if (i < text.Length && text[i] == '[')
                {
                    i++;
                    int valStart = i;
                    while (i < text.Length && text[i] != ']')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length) i += 2;
                        else i++;
                    }
                    string propValue = text[valStart..i];
                    properties[propName] = Unescape(propValue);

                    if (propName.Equals("SZ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (BoardSize.TryParse(propValue, out BoardSize parsedSize))
                        {
                            size = parsedSize;
                        }
                    }
                    else if (propName.Equals("RU", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Enum.TryParse(propValue, true, out GomokuVariant parsedVariant))
                        {
                            variant = parsedVariant;
                        }
                    }
                    else if (propName is "B" or "W")
                    {
                        PieceColor player = propName == "B" ? PieceColor.Black : PieceColor.White;
                        if (Point.TryParseSgf(propValue, size, out Point pt))
                        {
                            parsedMoves.Add(new Move(pt.X, pt.Y, player));
                            expectedPlayer = player.Opponent();
                        }
                    }
                }
            }
            i++;
        }

        moves = parsedMoves;
        return true;
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("]", "\\]");

    private static string Unescape(string text) =>
        text.Replace("\\]", "]").Replace("\\\\", "\\");
}

