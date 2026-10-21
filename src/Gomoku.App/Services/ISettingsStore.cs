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

    public GameSettings ToSettings()
    {
        BoardSize size = (BoardWidth, BoardHeight) switch
        {
            (15, 15) => BoardSize.Size15x15,
            (19, 19) => BoardSize.Size19x19,
            (20, 20) => BoardSize.Size20x20,
            (>= 5 and <= 20, >= 5 and <= 20) => new BoardSize(BoardWidth, BoardHeight),
            _ => BoardSize.Default
        };

        return new GameSettings
        {
            BoardSize = size,
            Variant = Enum.IsDefined(Variant) ? Variant : GomokuVariant.Freestyle,
            Mode = Enum.IsDefined(Mode) ? Mode : GameMode.HumanVsAi,
            AiEngine = Enum.IsDefined(AiEngine) ? AiEngine : AiEngineType.Carbon,
            TimeControl = Enum.IsDefined(TimeControl) ? TimeControl : TimeControlMode.TimePerMove,
            SearchDepth = Math.Clamp(SearchDepth, 1, 14),
            MoveTimeSeconds = Math.Clamp(MoveTimeSeconds, 1, 60),
            GameTimeMinutes = Math.Clamp(GameTimeMinutes, 1, 60),
            TranspositionTableEntries = TranspositionTableEntries >= 65_536 ? TranspositionTableEntries : TranspositionTable.DefaultEntries,
            UseOpeningBook = UseOpeningBook,
            SoundEnabled = SoundEnabled,
            CoordinatesVisible = CoordinatesVisible,
            GhostStoneEnabled = GhostStoneEnabled,
            LastMoveHighlightEnabled = LastMoveHighlightEnabled,
            WinLineHighlightEnabled = WinLineHighlightEnabled,
            ThemeName = ThemeName is "Wood" or "Dark" or "Classic" ? ThemeName : "Wood"
        };
    }
}

public interface ISettingsStore
{
    AppPreferences Load();
    bool Save(AppPreferences preferences);
}

