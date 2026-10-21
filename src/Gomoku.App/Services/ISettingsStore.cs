using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Services;

public sealed record AppPreferences(
    int BoardWidth = 15,
    int BoardHeight = 15,
    GomokuVariant Variant = GomokuVariant.Freestyle,
    GameMode Mode = GameMode.HumanVsAi,
    AiEngineType AiEngine = AiEngineType.Carbon,
    TimeControlMode TimeControl = TimeControlMode.TimePerMove,
    int SearchDepth = 6,
    int MoveTimeSeconds = 5,
    int GameTimeMinutes = 5,
    int TranspositionTableEntries = TranspositionTable.DefaultEntries,
    bool UseOpeningBook = true,
    bool SoundEnabled = true,
    bool CoordinatesVisible = true,
    bool GhostStoneEnabled = true,
    bool LastMoveHighlightEnabled = true,
    bool WinLineHighlightEnabled = true,
    string ThemeName = "Wood",
    bool ShowAnalysis = true)
{
    public static AppPreferences FromSettings(GameSettings settings, bool showAnalysis = true) => new(
        settings.BoardSize.Width,
        settings.BoardSize.Height,
        settings.Variant,
        settings.Mode,
        settings.AiEngine,
        settings.TimeControl,
        settings.SearchDepth,
        settings.MoveTimeSeconds,
        settings.GameTimeMinutes,
        settings.TranspositionTableEntries,
        settings.UseOpeningBook,
        settings.SoundEnabled,
        settings.CoordinatesVisible,
        settings.GhostStoneEnabled,
        settings.LastMoveHighlightEnabled,
        settings.WinLineHighlightEnabled,
        settings.ThemeName,
        showAnalysis);

    public GameSettings ToSettings() => new()
    {
        BoardSize = new BoardSize(BoardWidth, BoardHeight),
        Variant = Variant,
        Mode = Mode,
        AiEngine = AiEngine,
        TimeControl = TimeControl,
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

public interface ISettingsStore
{
    AppPreferences Load();
    bool Save(AppPreferences preferences);
}

