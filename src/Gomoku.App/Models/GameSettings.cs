using CommunityToolkit.Mvvm.ComponentModel;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Models;

/// <summary>
/// User-configurable application and gameplay settings.
/// </summary>
public sealed partial class GameSettings : ObservableObject
{
    [ObservableProperty]
    private BoardSize _boardSize = BoardSize.Default; // 15x15

    [ObservableProperty]
    private GomokuVariant _variant = GomokuVariant.Freestyle;

    [ObservableProperty]
    private GameMode _mode = GameMode.HumanVsAi;

    [ObservableProperty]
    private AiEngineType _aiEngine = AiEngineType.Carbon;

    [ObservableProperty]
    private TimeControlMode _timeControl = TimeControlMode.TimePerMove;

    [ObservableProperty]
    private int _searchDepth = 6;

    [ObservableProperty]
    private int _moveTimeSeconds = 5;

    [ObservableProperty]
    private int _gameTimeMinutes = 5;

    [ObservableProperty]
    private int _transpositionTableEntries = 1_048_576; // 2^20 (16 MiB)

    [ObservableProperty]
    private bool _useOpeningBook = true;

    [ObservableProperty]
    private bool _soundEnabled = true;

    [ObservableProperty]
    private bool _coordinatesVisible = true;

    [ObservableProperty]
    private bool _ghostStoneEnabled = true;

    [ObservableProperty]
    private bool _lastMoveHighlightEnabled = true;

    [ObservableProperty]
    private bool _winLineHighlightEnabled = true;

    [ObservableProperty]
    private string _themeName = "Wood";

    public GameSettings Clone() => new()
    {
        BoardSize = BoardSize,
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

