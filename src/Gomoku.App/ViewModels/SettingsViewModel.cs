using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly GameSettings _original;

    [ObservableProperty]
    private BoardSize _selectedBoardSize;

    [ObservableProperty]
    private GomokuVariant _selectedVariant;

    [ObservableProperty]
    private GameMode _selectedMode;

    [ObservableProperty]
    private AiEngineType _selectedAiEngine;

    [ObservableProperty]
    private TimeControlMode _selectedTimeControl;

    [ObservableProperty]
    private int _searchDepth;

    [ObservableProperty]
    private int _moveTimeSeconds;

    [ObservableProperty]
    private int _gameTimeMinutes;

    [ObservableProperty]
    private int _transpositionTableEntries;

    [ObservableProperty]
    private bool _useOpeningBook;

    [ObservableProperty]
    private bool _soundEnabled;

    [ObservableProperty]
    private bool _coordinatesVisible;

    [ObservableProperty]
    private bool _ghostStoneEnabled;

    [ObservableProperty]
    private bool _lastMoveHighlightEnabled;

    [ObservableProperty]
    private bool _winLineHighlightEnabled;

    [ObservableProperty]
    private string _themeName;

    public IReadOnlyList<BoardSize> AvailableBoardSizes { get; } = [
        BoardSize.Size15x15,
        BoardSize.Size19x19,
        BoardSize.Size20x20
    ];

    public IReadOnlyList<GomokuVariant> AvailableVariants { get; } = [
        GomokuVariant.Freestyle,
        GomokuVariant.Exact5,
        GomokuVariant.Renju
    ];

    public IReadOnlyList<GameMode> AvailableModes { get; } = [
        GameMode.HumanVsAi,
        GameMode.AiVsHuman,
        GameMode.HumanVsHuman,
        GameMode.AiVsAi
    ];

    public IReadOnlyList<AiEngineType> AvailableAiEngines { get; } = [
        AiEngineType.Carbon,
        AiEngineType.Simple
    ];

    public IReadOnlyList<TimeControlMode> AvailableTimeControls { get; } = [
        TimeControlMode.FixedDepth,
        TimeControlMode.TimePerMove,
        TimeControlMode.TimePerGame
    ];

    public IReadOnlyList<int> AvailableTtSizes { get; } = [
        65_536,
        262_144,
        1_048_576,
        4_194_304,
        16_777_216
    ];

    public IReadOnlyList<string> AvailableThemes { get; } = [
        "Wood",
        "Dark",
        "Classic"
    ];

    public bool IsSize15x15
    {
        get => SelectedBoardSize == BoardSize.Size15x15;
        set { if (value) { SelectedBoardSize = BoardSize.Size15x15; NotifyBoardSizeProperties(); } }
    }

    public bool IsSize19x19
    {
        get => SelectedBoardSize == BoardSize.Size19x19;
        set { if (value) { SelectedBoardSize = BoardSize.Size19x19; NotifyBoardSizeProperties(); } }
    }

    public bool IsSize20x20
    {
        get => SelectedBoardSize == BoardSize.Size20x20;
        set { if (value) { SelectedBoardSize = BoardSize.Size20x20; NotifyBoardSizeProperties(); } }
    }

    private void NotifyBoardSizeProperties()
    {
        OnPropertyChanged(nameof(IsSize15x15));
        OnPropertyChanged(nameof(IsSize19x19));
        OnPropertyChanged(nameof(IsSize20x20));
    }

    public bool IsFreestyle
    {
        get => SelectedVariant == GomokuVariant.Freestyle;
        set { if (value) { SelectedVariant = GomokuVariant.Freestyle; NotifyVariantProperties(); } }
    }

    public bool IsExact5
    {
        get => SelectedVariant == GomokuVariant.Exact5;
        set { if (value) { SelectedVariant = GomokuVariant.Exact5; NotifyVariantProperties(); } }
    }

    public bool IsRenju
    {
        get => SelectedVariant == GomokuVariant.Renju;
        set { if (value) { SelectedVariant = GomokuVariant.Renju; NotifyVariantProperties(); } }
    }

    private void NotifyVariantProperties()
    {
        OnPropertyChanged(nameof(IsFreestyle));
        OnPropertyChanged(nameof(IsExact5));
        OnPropertyChanged(nameof(IsRenju));
    }

    public bool IsCarbonEngine
    {
        get => SelectedAiEngine == AiEngineType.Carbon;
        set { if (value) { SelectedAiEngine = AiEngineType.Carbon; NotifyEngineProperties(); } }
    }

    public bool IsSimpleEngine
    {
        get => SelectedAiEngine == AiEngineType.Simple;
        set { if (value) { SelectedAiEngine = AiEngineType.Simple; NotifyEngineProperties(); } }
    }

    private void NotifyEngineProperties()
    {
        OnPropertyChanged(nameof(IsCarbonEngine));
        OnPropertyChanged(nameof(IsSimpleEngine));
    }

    public bool IsFixedDepth
    {
        get => SelectedTimeControl == TimeControlMode.FixedDepth;
        set { if (value) { SelectedTimeControl = TimeControlMode.FixedDepth; NotifyTimeControlProperties(); } }
    }

    public bool IsTimePerMove
    {
        get => SelectedTimeControl == TimeControlMode.TimePerMove;
        set { if (value) { SelectedTimeControl = TimeControlMode.TimePerMove; NotifyTimeControlProperties(); } }
    }

    public bool IsTimePerGame
    {
        get => SelectedTimeControl == TimeControlMode.TimePerGame;
        set { if (value) { SelectedTimeControl = TimeControlMode.TimePerGame; NotifyTimeControlProperties(); } }
    }

    private void NotifyTimeControlProperties()
    {
        OnPropertyChanged(nameof(IsFixedDepth));
        OnPropertyChanged(nameof(IsTimePerMove));
        OnPropertyChanged(nameof(IsTimePerGame));
    }

    public int MinTranspositionTablePower => 16;
    public int MaxTranspositionTablePower => 24;

    public int TranspositionTablePower
    {
        get
        {
            int power = System.Numerics.BitOperations.Log2((uint)Math.Max(65_536, TranspositionTableEntries));
            return Math.Clamp(power, MinTranspositionTablePower, MaxTranspositionTablePower);
        }
        set
        {
            int power = Math.Clamp(value, MinTranspositionTablePower, MaxTranspositionTablePower);
            TranspositionTableEntries = 1 << power;
            OnPropertyChanged(nameof(TranspositionTablePower));
            OnPropertyChanged(nameof(TranspositionTableEntriesText));
        }
    }

    public string TranspositionTableEntriesText => TranspositionTableEntries switch
    {
        <= 65_536 => "65,536 (1 MB)",
        <= 262_144 => "262,144 (4 MB)",
        <= 1_048_576 => "1,048,576 (16 MB)",
        <= 4_194_304 => "4,194,304 (64 MB)",
        _ => "16,777,216 (256 MB)"
    };

    partial void OnSelectedBoardSizeChanged(BoardSize value) => NotifyBoardSizeProperties();
    partial void OnSelectedVariantChanged(GomokuVariant value) => NotifyVariantProperties();
    partial void OnSelectedAiEngineChanged(AiEngineType value) => NotifyEngineProperties();
    partial void OnSelectedTimeControlChanged(TimeControlMode value) => NotifyTimeControlProperties();
    partial void OnTranspositionTableEntriesChanged(int value)
    {
        OnPropertyChanged(nameof(TranspositionTablePower));
        OnPropertyChanged(nameof(TranspositionTableEntriesText));
    }

    public event Action? Saved;
    public event Action? Cancelled;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ResetDefaultsCommand { get; }

    public SettingsViewModel(GameSettings settings)
    {
        _original = settings;
        _selectedBoardSize = settings.BoardSize;
        _selectedVariant = settings.Variant;
        _selectedMode = settings.Mode;
        _selectedAiEngine = settings.AiEngine;
        _selectedTimeControl = settings.TimeControl;
        _searchDepth = settings.SearchDepth;
        _moveTimeSeconds = settings.MoveTimeSeconds;
        _gameTimeMinutes = settings.GameTimeMinutes;
        _transpositionTableEntries = settings.TranspositionTableEntries;
        _useOpeningBook = settings.UseOpeningBook;
        _soundEnabled = settings.SoundEnabled;
        _coordinatesVisible = settings.CoordinatesVisible;
        _ghostStoneEnabled = settings.GhostStoneEnabled;
        _lastMoveHighlightEnabled = settings.LastMoveHighlightEnabled;
        _winLineHighlightEnabled = settings.WinLineHighlightEnabled;
        _themeName = settings.ThemeName;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
    }

    private void Save()
    {
        _original.BoardSize = SelectedBoardSize;
        _original.Variant = SelectedVariant;
        _original.Mode = SelectedMode;
        _original.AiEngine = SelectedAiEngine;
        _original.TimeControl = SelectedTimeControl;
        _original.SearchDepth = SearchDepth;
        _original.MoveTimeSeconds = MoveTimeSeconds;
        _original.GameTimeMinutes = GameTimeMinutes;
        _original.TranspositionTableEntries = TranspositionTableEntries;
        _original.UseOpeningBook = UseOpeningBook;
        _original.SoundEnabled = SoundEnabled;
        _original.CoordinatesVisible = CoordinatesVisible;
        _original.GhostStoneEnabled = GhostStoneEnabled;
        _original.LastMoveHighlightEnabled = LastMoveHighlightEnabled;
        _original.WinLineHighlightEnabled = WinLineHighlightEnabled;
        _original.ThemeName = ThemeName;

        Saved?.Invoke();
    }

    private void Cancel()
    {
        Cancelled?.Invoke();
    }

    private void ResetDefaults()
    {
        GameSettings def = new();
        SelectedBoardSize = def.BoardSize;
        SelectedVariant = def.Variant;
        SelectedMode = def.Mode;
        SelectedAiEngine = def.AiEngine;
        SelectedTimeControl = def.TimeControl;
        SearchDepth = def.SearchDepth;
        MoveTimeSeconds = def.MoveTimeSeconds;
        GameTimeMinutes = def.GameTimeMinutes;
        TranspositionTableEntries = def.TranspositionTableEntries;
        UseOpeningBook = def.UseOpeningBook;
        SoundEnabled = def.SoundEnabled;
        CoordinatesVisible = def.CoordinatesVisible;
        GhostStoneEnabled = def.GhostStoneEnabled;
        LastMoveHighlightEnabled = def.LastMoveHighlightEnabled;
        WinLineHighlightEnabled = def.WinLineHighlightEnabled;
        ThemeName = def.ThemeName;
    }
}

