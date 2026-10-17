using Gomoku.Core.Models;

namespace Gomoku.Core.Formats;

/// <summary>
/// Auto-detecting file serializer supporting both SGF (.sgf) and Gomoku text format (.gomoku, .txt).
/// </summary>
public static class GameFileSerializer
{
    public static bool TryParse(
        string fileContent,
        out IReadOnlyList<Move> moves,
        out BoardSize size,
        out GomokuVariant variant,
        out Dictionary<string, string> metadata)
    {
        string trimmed = fileContent.TrimStart();
        if (trimmed.StartsWith("(;"))
        {
            return SgfFormat.TryParse(fileContent, out moves, out size, out variant, out metadata);
        }

        return GomokuRecordFormat.TryParse(fileContent, out moves, out size, out variant, out metadata);
    }
}

