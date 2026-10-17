using Gomoku.Core.Models;

namespace Gomoku.App.Models;

/// <summary>
/// User-configurable application and gameplay settings.
/// </summary>
public sealed class GameSettings
{
    public BoardSize BoardSize { get; set; } = BoardSize.Default; // 15x15
    public GomokuVariant Variant { get; set; } = GomokuVariant.Freestyle;
    public GameMode Mode { get; set; } = GameMode.HumanVsAi;
    public AiEngineType AiEngine { get; set; } = AiEngineType.Carbon;

    public int SearchDepth { get; set; } = 6;
    public int MoveTimeSeconds { get; set; } = 5;
    public int GameTimeMinutes { get; set; } = 5;
    public int TranspositionTableEntries { get; set; } = 1_048_576; // 2^20 (16 MiB)
    public bool UseOpeningBook { get; set; } = true;

    public bool SoundEnabled { get; set; } = true;
    public bool CoordinatesVisible { get; set; } = true;
    public bool GhostStoneEnabled { get; set; } = true;
    public bool LastMoveHighlightEnabled { get; set; } = true;
    public bool WinLineHighlightEnabled { get; set; } = true;
    public string ThemeName { get; set; } = "Wood";

    public GameSettings Clone() => new()
    {
        BoardSize = BoardSize,
        Variant = Variant,
        Mode = Mode,
        AiEngine = AiEngine,
        SearchDepth = SearchDepth,
        MoveTimeSeconds = MoveTimeSeconds,
        GameTimeMinutes = GameTimeMinutes,
        TranspositionTableEntries = TranspositionTableEntries,
        UseOpeningBook = UseOpeningBook,
        SoundEnabled = SoundEnabled,
        CoordinatesVisible = CoordinatesVisible,
        GhostStoneEnabled = GhostStoneEnabled,
        LastMoveHighlightEnabled = LastMoveHighlightEnabled,
        WinLineHighlightEnabled = WinLineHighlightEnabled,
        ThemeName = ThemeName
    };
}

