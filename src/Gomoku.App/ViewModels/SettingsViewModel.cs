using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gomoku.App.Models;
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

