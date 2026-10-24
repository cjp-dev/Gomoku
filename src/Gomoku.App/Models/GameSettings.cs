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
    private BoardSize _boardSize = BoardSize.Size15x15;

    [ObservableProperty]
    private GomokuVariant _variant = GomokuVariant.Freestyle;

    [ObservableProperty]
    private GameMode _mode = GameMode.HumanVsAi;

    [ObservableProperty]
    private AiEngineType _aiEngine = AiEngineType.Carbon;

    [ObservableProperty]
    private bool _multiThreadingEnabled = true;

    [ObservableProperty]
    private int _threadCount = 0; // 0 = Auto (Environment.ProcessorCount)

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
        MultiThreadingEnabled = MultiThreadingEnabled,
        ThreadCount = ThreadCount,
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

    public string Summary => AiEngine switch
    {
        AiEngineType.Simple => "Simple AI",
        _ => TimeControl switch
        {
            TimeControlMode.FixedDepth => $"{SearchDepth} plies",
            TimeControlMode.TimePerMove => $"{MoveTimeSeconds}s / move",
            _ => $"{GameTimeMinutes} min / game",
        }
    };

    public string VariantBadgeText => Variant switch
    {
        GomokuVariant.Freestyle => "Freestyle",
        GomokuVariant.Exact5 => "Standard",
        GomokuVariant.Renju => "Renju",
        _ => Variant.ToString()
    };
}

