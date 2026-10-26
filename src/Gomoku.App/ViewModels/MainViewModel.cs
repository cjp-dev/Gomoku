using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Formats;
using Gomoku.Core.Models;

namespace Gomoku.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IGameFileService _fileService;
    private readonly ISoundService _soundService;
    private readonly LocalEngineHost _localEngineHost = new();
    private readonly IEngineHost _engineHost;
    private readonly ISettingsStore? _settingsStore;
    private readonly bool _settingsLoaded;

    private CancellationTokenSource? _aiCts;
    private CancellationTokenSource? _moveNowCts;
    private CancellationTokenSource? _clockCts;
    private long _lastClockTickMs;

    public IEngineHost EngineHost => _engineHost;

    /// <summary>
    /// Minimum time in milliseconds before the computer plays, so instant moves (book moves, forced moves) feel natural.
    /// Searches that take longer are not delayed. Tests can set it to 0.
    /// </summary>
    public int AiDelayMs { get; set; } = 250;

    public Func<GameSettings, IPlayer>? AiPlayerFactory
    {
        get => _localEngineHost.PlayerFactory;
        set => _localEngineHost.PlayerFactory = value;
    }

    public GameSession Session { get; }
    public GameSettings Settings { get; }
    public AnalysisViewModel Analysis { get; } = new();

    public event Action? BoardLayoutChanged;

    public ObservableCollection<IntersectionViewModel> Intersections { get; } = [];
    private IntersectionViewModel[,] _grid = new IntersectionViewModel[0, 0];

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private string _statusText = "Ready to play";

    [ObservableProperty]
    private string _moveHistoryText = "";

    [ObservableProperty]
    private PieceColor _sideToMove = PieceColor.Black;

    [ObservableProperty]
    private GameStatus _gameStatus = GameStatus.InProgress;

    [ObservableProperty]
    private int _moveCount;

    [ObservableProperty]
    private TimeSpan _blackRemainingTime;

    [ObservableProperty]
    private TimeSpan _whiteRemainingTime;

    public string BlackTimeFormatted => BlackRemainingTime.ToString(@"mm\:ss");
    public string WhiteTimeFormatted => WhiteRemainingTime.ToString(@"mm\:ss");

    public bool CanUndo => Session.CanUndo && !IsThinking;
    public bool CanRedo => Session.CanRedo && !IsThinking;
    public bool CanHoverMove => !IsThinking && !GameStatus.IsGameOver() && IsHumanTurn();

    [ObservableProperty]
    private bool _isAnalysisVisible = true;

    [ObservableProperty]
    private bool _isGameModified;

    private bool _hasHumanContinuedOnTime;
    private volatile bool _isTimeoutPrompting;
    private readonly Dictionary<int, (TimeSpan Black, TimeSpan White)> _timeAtPly = [];

    public bool IsStatusModified => IsVsComputer && IsGameModified;

    partial void OnIsGameModifiedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStatusModified));
    }

    public ObservableCollection<MoveTurnRecord> MoveTurns { get; } = [];

    public string BlackPlayerLabel => Settings.Mode switch
    {
        GameMode.AiVsHuman or GameMode.AiVsAi => "Computer",
        GameMode.HumanVsHuman => "Player 1",
        _ => "Human"
    };

    public string WhitePlayerLabel => Settings.Mode switch
    {
        GameMode.HumanVsAi or GameMode.AiVsAi => "Computer",
        GameMode.HumanVsHuman => "Player 2",
        _ => "Human"
    };

    public int BlackPiecesCount => (Session.MoveHistory.Count + 1) / 2;
    public int WhitePiecesCount => Session.MoveHistory.Count / 2;

    public string VariantBadgeText => Settings.VariantBadgeText;

    public bool IsClockVisible => Settings.TimeControl == TimeControlMode.TimePerGame;
    public string ClockText => SideToMove == PieceColor.Black ? BlackTimeFormatted : WhiteTimeFormatted;

    public bool IsHumanVsComputer => Settings.Mode == GameMode.HumanVsAi;
    public bool IsComputerVsHuman => Settings.Mode == GameMode.AiVsHuman;
    public bool IsVsComputer => Settings.Mode is GameMode.HumanVsAi or GameMode.AiVsHuman or GameMode.AiVsAi;
    public bool IsHumanVsHuman => Settings.Mode == GameMode.HumanVsHuman;

    public bool IsGameSettingVisible => IsVsComputer;
    public string SettingsBadgeText => Settings.Summary;
    public string MoveCountText => Session.MoveHistory.Count == 1 ? "1 move" : $"{Session.MoveHistory.Count} moves";

    public bool CanSwitchSides =>
        IsVsComputer &&
        IsHumanTurn() &&
        !GameStatus.IsGameOver() &&
        !IsThinking;

    public bool IsBlackActiveTurn => SideToMove == PieceColor.Black && !GameStatus.IsGameOver();
    public bool IsWhiteActiveTurn => SideToMove == PieceColor.White && !GameStatus.IsGameOver();

    public bool IsSoundOn => Settings.SoundEnabled;
    public bool IsCoordinatesVisible => Settings.CoordinatesVisible;
    public bool IsGhostStoneOn => Settings.GhostStoneEnabled;
    public bool IsLastMoveHighlightOn => Settings.LastMoveHighlightEnabled;
    public bool IsWinLineHighlightOn => Settings.WinLineHighlightEnabled;

    public IRelayCommand NewGameCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand SwitchSidesCommand { get; }
    public IRelayCommand MoveNowCommand { get; }
    public IRelayCommand OpenFileCommand { get; }
    public IRelayCommand SaveFileCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenAboutCommand { get; }
    public IRelayCommand CancelSearchCommand { get; }
    public IRelayCommand ToggleAnalysisCommand { get; }
    public IRelayCommand ToggleSoundCommand { get; }
    public IRelayCommand ToggleCoordinatesCommand { get; }
    public IRelayCommand ToggleGhostStoneCommand { get; }
    public IRelayCommand ToggleLastMoveHighlightCommand { get; }
    public IRelayCommand ToggleWinLineHighlightCommand { get; }
    public IRelayCommand<GameMode> SetGameModeCommand { get; }

    public MainViewModel(
        IDialogService dialogService,
        IGameFileService fileService,
        ISoundService soundService,
        GameSettings? settings = null,
        IEngineHost? engineHost = null,
        ISettingsStore? settingsStore = null)
    {
        _dialogService = dialogService;
        _fileService = fileService;
        _soundService = soundService;
        _engineHost = engineHost ?? _localEngineHost;
        _settingsStore = settingsStore;

        if (_settingsStore != null)
        {
            AppPreferences saved = _settingsStore.Load();
            Settings = saved.ToSettings();
            IsAnalysisVisible = saved.ShowAnalysis;
            _soundService.IsMuted = !saved.SoundEnabled;
        }
        else
        {
            Settings = settings ?? new GameSettings();
            _soundService.IsMuted = !Settings.SoundEnabled;
        }
        _settingsLoaded = true;

        Session = new GameSession(Settings.BoardSize, Settings.Variant);

        NewGameCommand = new AsyncRelayCommand(NewGameAsync);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        SwitchSidesCommand = new RelayCommand(SwitchSides, () => CanSwitchSides);
        MoveNowCommand = new RelayCommand(MoveNow, () => IsThinking);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAsync);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        CancelSearchCommand = new RelayCommand(CancelAiSearch);
        ToggleAnalysisCommand = new RelayCommand(() =>
        {
            IsAnalysisVisible = !IsAnalysisVisible;
            SavePreferences();
        });
        ToggleSoundCommand = new RelayCommand(() =>
        {
            Settings.SoundEnabled = !Settings.SoundEnabled;
            _soundService.IsMuted = !Settings.SoundEnabled;
            OnPropertyChanged(nameof(IsSoundOn));
            SavePreferences();
        });
        ToggleCoordinatesCommand = new RelayCommand(() =>
        {
            Settings.CoordinatesVisible = !Settings.CoordinatesVisible;
            OnPropertyChanged(nameof(IsCoordinatesVisible));
            BoardLayoutChanged?.Invoke();
            SavePreferences();
        });
        ToggleGhostStoneCommand = new RelayCommand(() =>
        {
            Settings.GhostStoneEnabled = !Settings.GhostStoneEnabled;
            OnPropertyChanged(nameof(IsGhostStoneOn));
            SavePreferences();
        });
        ToggleLastMoveHighlightCommand = new RelayCommand(() =>
        {
            Settings.LastMoveHighlightEnabled = !Settings.LastMoveHighlightEnabled;
            OnPropertyChanged(nameof(IsLastMoveHighlightOn));
            UpdateUiState();
            BoardLayoutChanged?.Invoke();
            SavePreferences();
        });
        ToggleWinLineHighlightCommand = new RelayCommand(() =>
        {
            Settings.WinLineHighlightEnabled = !Settings.WinLineHighlightEnabled;
            OnPropertyChanged(nameof(IsWinLineHighlightOn));
            UpdateUiState();
            BoardLayoutChanged?.Invoke();
            SavePreferences();
        });
        SetGameModeCommand = new AsyncRelayCommand<GameMode>(SetGameModeAsync);

        ResetBoard(Settings.BoardSize);
    }

    public void MoveNow() => _moveNowCts?.Cancel();

    /// <summary>Switches the board theme ("Wood", "Dark" or "Classic") from the Options menu.</summary>
    public void SetBoardTheme(string themeName)
    {
        if (Settings.ThemeName == themeName) return;

        Settings.ThemeName = themeName;
        OnPropertyChanged(nameof(Settings));
        BoardLayoutChanged?.Invoke();
        SavePreferences();
    }

    private void SavePreferences()
    {
        if (_settingsLoaded)
        {
            _settingsStore?.Save(AppPreferences.FromSettings(Settings, IsAnalysisVisible));
        }
    }

    public async Task SetGameModeAsync(GameMode mode)
    {
        if (Settings.Mode == mode) return;

        if (!GameStatus.IsGameOver() && Session.MoveHistory.Count > 0)
        {
            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Abandon Match",
                "A game is currently in progress. Changing game mode will start a new game.\n\nDo you want to abandon the current match and start a new game?");
            if (!confirm) return;
        }

        SetGameMode(mode);
        ResetBoard(Settings.BoardSize);
    }

    public void SetGameMode(GameMode mode)
    {
        if (Settings.Mode != mode)
        {
            Settings.Mode = mode;
            NotifyModePropertiesChanged();
            SavePreferences();
        }
    }

    public void SwitchSides()
    {
        if (!CanSwitchSides) return;

        CancelAiSearch();
        Settings.Mode = Settings.Mode == GameMode.HumanVsAi
            ? GameMode.AiVsHuman
            : GameMode.HumanVsAi;

        IsGameModified = true;
        NotifyModePropertiesChanged();
        SavePreferences();
        UpdateUiState();

        if (!GameStatus.IsGameOver() && !IsHumanTurn())
        {
            CheckAndTriggerAiMove();
        }
    }

    private void NotifyModePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(IsComputerVsHuman));
        OnPropertyChanged(nameof(IsVsComputer));
        OnPropertyChanged(nameof(IsHumanVsHuman));
        OnPropertyChanged(nameof(BlackPlayerLabel));
        OnPropertyChanged(nameof(WhitePlayerLabel));
        OnPropertyChanged(nameof(CanHoverMove));
        OnPropertyChanged(nameof(CanSwitchSides));
        OnPropertyChanged(nameof(IsGameSettingVisible));
        OnPropertyChanged(nameof(SettingsBadgeText));
        OnPropertyChanged(nameof(IsStatusModified));
        SwitchSidesCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSideToMoveChanged(PieceColor value)
    {
        OnPropertyChanged(nameof(IsBlackActiveTurn));
        OnPropertyChanged(nameof(IsWhiteActiveTurn));
        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(CanSwitchSides));
        SwitchSidesCommand?.NotifyCanExecuteChanged();
    }

    partial void OnGameStatusChanged(GameStatus value)
    {
        OnPropertyChanged(nameof(IsBlackActiveTurn));
        OnPropertyChanged(nameof(IsWhiteActiveTurn));
        OnPropertyChanged(nameof(CanHoverMove));
        OnPropertyChanged(nameof(CanSwitchSides));
        SwitchSidesCommand?.NotifyCanExecuteChanged();
    }

    partial void OnBlackRemainingTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(BlackTimeFormatted));
        if (SideToMove == PieceColor.Black) OnPropertyChanged(nameof(ClockText));
    }

    partial void OnWhiteRemainingTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(WhiteTimeFormatted));
        if (SideToMove == PieceColor.White) OnPropertyChanged(nameof(ClockText));
    }

    partial void OnIsThinkingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanHoverMove));
        OnPropertyChanged(nameof(CanSwitchSides));
        UndoCommand?.NotifyCanExecuteChanged();
        RedoCommand?.NotifyCanExecuteChanged();
        MoveNowCommand?.NotifyCanExecuteChanged();
        SwitchSidesCommand?.NotifyCanExecuteChanged();
    }

    public void ResetBoard(BoardSize size, bool triggerAi = true)
    {
        CancelAiSearch();
        StopClock();
        Session.Reset(size, Settings.Variant, PieceColor.Black);

        TimeSpan initialClock = TimeSpan.FromMinutes(Math.Max(1, Settings.GameTimeMinutes));
        BlackRemainingTime = initialClock;
        WhiteRemainingTime = initialClock;

        _timeAtPly.Clear();
        _timeAtPly[0] = (initialClock, initialClock);
        IsGameModified = false;
        _hasHumanContinuedOnTime = false;

        Intersections.Clear();
        _grid = new IntersectionViewModel[size.Width, size.Height];
        var starPoints = size.GetStarPoints();

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                bool isStar = starPoints.Contains(new Point(x, y));
                var vm = new IntersectionViewModel(x, y, isStar, OnIntersectionClicked);
                _grid[x, y] = vm;
                Intersections.Add(vm);
            }
        }

        Analysis.Reset();
        UpdateUiState();
        BoardLayoutChanged?.Invoke();
        _ = _soundService.PlaySoundAsync(SoundType.GameStart);

        if (Settings.TimeControl == TimeControlMode.TimePerGame)
        {
            StartClock();
        }

        if (triggerAi)
        {
            CheckAndTriggerAiMove();
        }
    }

    private void StartClock()
    {
        StopClock();
        _lastClockTickMs = Environment.TickCount64;
        _clockCts = new CancellationTokenSource();
        CancellationToken token = _clockCts.Token;

        _ = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(250));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (GameStatus.IsGameOver())
                    {
                        StopClock();
                        break;
                    }

                    long now = Environment.TickCount64;
                    long elapsedMs = now - _lastClockTickMs;
                    _lastClockTickMs = now;

                    // The clocks start with the first move and pause while the timeout dialog is open.
                    if (elapsedMs <= 0 || _isTimeoutPrompting || Session.MoveHistory.Count == 0) continue;

                    if (SideToMove == PieceColor.Black)
                    {
                        if (IsHumanPlayer(PieceColor.Black) && _hasHumanContinuedOnTime)
                        {
                            BlackRemainingTime = TimeSpan.Zero;
                        }
                        else
                        {
                            TimeSpan next = BlackRemainingTime - TimeSpan.FromMilliseconds(elapsedMs);
                            if (next <= TimeSpan.Zero)
                            {
                                BlackRemainingTime = TimeSpan.Zero;
                                // Keep the loop running: if the human continues, the computer's clock must still tick.
                                _ = HandleTimeoutAsync(PieceColor.Black);
                            }
                            else
                            {
                                BlackRemainingTime = next;
                            }
                        }
                    }
                    else if (SideToMove == PieceColor.White)
                    {
                        if (IsHumanPlayer(PieceColor.White) && _hasHumanContinuedOnTime)
                        {
                            WhiteRemainingTime = TimeSpan.Zero;
                        }
                        else
                        {
                            TimeSpan next = WhiteRemainingTime - TimeSpan.FromMilliseconds(elapsedMs);
                            if (next <= TimeSpan.Zero)
                            {
                                WhiteRemainingTime = TimeSpan.Zero;
                                _ = HandleTimeoutAsync(PieceColor.White);
                            }
                            else
                            {
                                WhiteRemainingTime = next;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void StopClock()
    {
        if (_clockCts != null)
        {
            _clockCts.Cancel();
            _clockCts.Dispose();
            _clockCts = null;
        }
    }

    private bool IsHumanPlayer(PieceColor color) =>
        (Settings.Mode == GameMode.HumanVsAi && color == PieceColor.Black) ||
        (Settings.Mode == GameMode.AiVsHuman && color == PieceColor.White);

    public void HandleTimeout(PieceColor timedOutPlayer) => _ = HandleTimeoutAsync(timedOutPlayer);

    public async Task HandleTimeoutAsync(PieceColor timedOutPlayer)
    {
        if (GameStatus.IsGameOver()) return;

        if (IsVsComputer && IsHumanPlayer(timedOutPlayer) && !_hasHumanContinuedOnTime)
        {
            if (_isTimeoutPrompting) return;

            bool continuePlaying;
            _isTimeoutPrompting = true;
            try
            {
                continuePlaying = await _dialogService.ShowConfirmationAsync(
                    "Time Expired",
                    "You ran out of time!\n\nDo you want to continue playing?");
            }
            finally
            {
                _isTimeoutPrompting = false;
            }

            if (continuePlaying)
            {
                _hasHumanContinuedOnTime = true;
                IsGameModified = true;
                if (timedOutPlayer == PieceColor.Black)
                    BlackRemainingTime = TimeSpan.Zero;
                else
                    WhiteRemainingTime = TimeSpan.Zero;

                UpdateUiState();
                return;
            }
        }

        CancelAiSearch();
        StopClock();
        GameStatus = timedOutPlayer == PieceColor.Black ? GameStatus.WhiteWon : GameStatus.BlackWon;
        StatusText = $"{timedOutPlayer} ran out of time! {timedOutPlayer.Opponent()} won!";
        OnPropertyChanged(nameof(CanHoverMove));
        _ = _soundService.PlaySoundAsync(IsHumanPlayer(timedOutPlayer) ? SoundType.Loss : SoundType.Win);
    }

    public void OnIntersectionClicked(IntersectionViewModel cell)
    {
        if (IsThinking || GameStatus.IsGameOver()) return;
        if (!IsHumanTurn()) return;

        Move move = new(cell.X, cell.Y, Session.CurrentState.SideToMove);
        if (!RuleEngine.Instance.IsMoveLegal(Session.CurrentState, move, out string? reason))
        {
            StatusText = reason ?? "Illegal move";
            _ = _soundService.PlaySoundAsync(SoundType.IllegalMove);
            return;
        }

        Session.ApplyMove(move);
        _timeAtPly[Session.MoveHistory.Count] = (BlackRemainingTime, WhiteRemainingTime);
        _ = _soundService.PlaySoundAsync(SoundType.StonePlaced);
        UpdateUiState();

        if (GameStatus.IsGameOver())
        {
            HandleGameOver();
        }
        else
        {
            Analysis.ConsumePlyAfterMove();
            CheckAndTriggerAiMove();
        }
    }

    public bool IsHumanTurn() => Settings.Mode switch
    {
        GameMode.HumanVsAi => Session.CurrentState.SideToMove == PieceColor.Black,
        GameMode.AiVsHuman => Session.CurrentState.SideToMove == PieceColor.White,
        GameMode.HumanVsHuman => true,
        GameMode.AiVsAi => false,
        _ => true
    };

    private void CheckAndTriggerAiMove()
    {
        if (GameStatus.IsGameOver()) return;

        bool isAiTurn = Settings.Mode switch
        {
            GameMode.HumanVsAi => Session.CurrentState.SideToMove == PieceColor.White,
            GameMode.AiVsHuman => Session.CurrentState.SideToMove == PieceColor.Black,
            GameMode.AiVsAi => true,
            GameMode.HumanVsHuman => false,
            _ => false
        };

        if (isAiTurn)
        {
            _ = RunAiTurnAsync();
        }
    }

    private SearchLimits CreateSearchLimits()
    {
        return Settings.TimeControl switch
        {
            TimeControlMode.FixedDepth => SearchLimits.FixedDepth(
                Math.Max(1, Settings.SearchDepth),
                Settings.UseOpeningBook),
            TimeControlMode.TimePerGame => SearchLimits.TimePerGame(
                Session.CurrentState.SideToMove == PieceColor.Black ? BlackRemainingTime : WhiteRemainingTime,
                Settings.UseOpeningBook),
            _ => SearchLimits.TimePerMove(
                TimeSpan.FromSeconds(Math.Max(1, Settings.MoveTimeSeconds)),
                Settings.UseOpeningBook)
        };
    }

    private int _searchId;

    // Instant moves (book moves, forced moves) wait until AiDelayMs has passed since the search started.
    // Moves that took longer are played at once, and Move Now skips the wait.
    private async Task WaitForNaturalDelayAsync(TimeSpan searchTime, CancellationToken cancel, CancellationToken moveNow)
    {
        int remainingMs = AiDelayMs - (int)searchTime.TotalMilliseconds;
        if (remainingMs <= 0 || moveNow.IsCancellationRequested)
        {
            return;
        }

        using var delay = CancellationTokenSource.CreateLinkedTokenSource(cancel, moveNow);
        try
        {
            await Task.Delay(remainingMs, delay.Token);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            // Move Now: play the move at once
        }
    }

    private async Task RunAiTurnAsync()
    {
        if (IsThinking || GameStatus.IsGameOver()) return;

        IsThinking = true;
        StatusText = $"{Session.CurrentState.SideToMove} is thinking...";
        using CancellationTokenSource aiCts = new();
        using CancellationTokenSource moveNowCts = new();
        _aiCts = aiCts;
        _moveNowCts = moveNowCts;
        int searchId = ++_searchId;
        Analysis.Reset();
        bool continueAiVsAi = false;

        try
        {
            var legalMoves = RuleEngine.Instance.GetLegalMoves(Session.CurrentState);
            if (legalMoves.Count == 0)
            {
                return;
            }

            SearchLimits limits = CreateSearchLimits();

            var progress = new Progress<SearchAnalysis>(a =>
            {
                if (searchId == _searchId && IsThinking && !aiCts.IsCancellationRequested)
                {
                    Analysis.Update(a);
                }
            });

            long searchStart = Stopwatch.GetTimestamp();
            var (bestMove, lastAnalysis) = await _engineHost.ChooseMoveAsync(
                Session.CurrentState,
                limits,
                legalMoves,
                Settings,
                progress,
                aiCts.Token,
                moveNowCts.Token);

            if (searchId != _searchId || aiCts.IsCancellationRequested || GameStatus.IsGameOver()) return;

            if (lastAnalysis != null)
            {
                Analysis.Update(lastAnalysis);
            }

            await WaitForNaturalDelayAsync(Stopwatch.GetElapsedTime(searchStart), aiCts.Token, moveNowCts.Token);
            if (searchId != _searchId || aiCts.IsCancellationRequested || GameStatus.IsGameOver()) return;

            if (Session.ApplyMove(bestMove))
            {
                _timeAtPly[Session.MoveHistory.Count] = (BlackRemainingTime, WhiteRemainingTime);
                await _soundService.PlaySoundAsync(SoundType.StonePlaced);
                UpdateUiState();

                if (GameStatus.IsGameOver())
                {
                    HandleGameOver();
                }
                else
                {
                    Analysis.ConsumePlyAfterMove();
                    if (Settings.Mode == GameMode.AiVsAi)
                    {
                        await Task.Delay(200, aiCts.Token);
                        continueAiVsAi = searchId == _searchId && !aiCts.IsCancellationRequested;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (searchId == _searchId)
            {
                StatusText = "Search stopped";
            }
        }
        catch (Exception ex)
        {
            if (searchId == _searchId)
            {
                StatusText = $"Engine error: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_aiCts, aiCts)) _aiCts = null;
            if (ReferenceEquals(_moveNowCts, moveNowCts)) _moveNowCts = null;

            if (searchId == _searchId)
            {
                IsThinking = false;
                UpdateUiState();
            }
        }

        if (continueAiVsAi)
        {
            CheckAndTriggerAiMove();
        }
    }

    public void CancelAiSearch()
    {
        _searchId++;
        _moveNowCts?.Cancel();
        _moveNowCts = null;
        _aiCts?.Cancel();
        _aiCts = null;
        IsThinking = false;
    }

    public async Task NewGameAsync()
    {
        if (!GameStatus.IsGameOver() && Session.MoveHistory.Count > 0)
        {
            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Abandon Match",
                "A game is currently in progress. Abandon current match and start a new game?");
            if (!confirm) return;
        }

        NewGame();
    }

    public void NewGame()
    {
        ResetBoard(Settings.BoardSize);
    }

    public void Undo()
    {
        if (IsThinking) CancelAiSearch();

        if (IsVsComputer)
        {
            IsGameModified = true;
        }

        // In HumanVsAi, undo 2 moves (both AI and human) if it is human turn
        if (Settings.Mode == GameMode.HumanVsAi && Session.MoveHistory.Count >= 2 && Session.CurrentState.SideToMove == PieceColor.Black)
        {
            Session.Undo();
            Session.Undo();
        }
        else if (Settings.Mode == GameMode.AiVsHuman && Session.MoveHistory.Count >= 2 && Session.CurrentState.SideToMove == PieceColor.White)
        {
            Session.Undo();
            Session.Undo();
        }
        else
        {
            Session.Undo();
        }

        int currentPly = Session.MoveHistory.Count;
        if (_timeAtPly.TryGetValue(currentPly, out var restoredTime))
        {
            BlackRemainingTime = restoredTime.Black;
            WhiteRemainingTime = restoredTime.White;
        }

        UpdateUiState();
        Analysis.Reset();
        _ = _soundService.PlaySoundAsync(SoundType.Undo);
    }

    public void Redo()
    {
        if (IsThinking) return;

        if (Settings.Mode == GameMode.HumanVsAi && Session.RedoCount >= 2)
        {
            Session.Redo();
            Session.Redo();
        }
        else if (Settings.Mode == GameMode.AiVsHuman && Session.RedoCount >= 2)
        {
            Session.Redo();
            Session.Redo();
        }
        else
        {
            Session.Redo();
        }

        UpdateUiState();
        _ = _soundService.PlaySoundAsync(SoundType.StonePlaced);
    }

    private void UpdateUiState()
    {
        BoardState state = Session.CurrentState;
        SideToMove = state.SideToMove;
        GameStatus = state.Status;
        MoveCount = state.MoveCount;

        Move? lastMove = Session.MoveHistory.Count > 0 ? Session.MoveHistory[^1] : null;
        HashSet<Point> winningPoints = new(state.WinningLine);

        PatternBoard? renjuBoard = null;
        if (!state.Status.IsGameOver() && state.Variant == GomokuVariant.Renju && state.SideToMove == state.FirstPlayer)
        {
            renjuBoard = RuleEngine.BuildPatternBoard(state);
        }

        // Refresh all grid cells
        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                var vm = _grid[x, y];
                vm.Piece = state.GetCell(x, y);
                vm.GhostPiece = state.SideToMove;
                vm.IsLastMove = Settings.LastMoveHighlightEnabled && lastMove.HasValue && lastMove.Value.X == x && lastMove.Value.Y == y;
                vm.IsWinningLine = Settings.WinLineHighlightEnabled && winningPoints.Contains(new Point(x, y));
                vm.IsForbidden = renjuBoard != null && vm.Piece == PieceColor.Empty && renjuBoard.IsForbidden(x, y, state.SideToMove);
                if (!CanHoverMove || vm.HasPiece)
                {
                    vm.IsHovered = false;
                }
            }
        }

        // Format history
        MoveHistoryText = FormatMoveHistory(Session.MoveHistory);

        MoveTurns.Clear();
        for (int i = 0; i < Session.MoveHistory.Count; i += 2)
        {
            int turn = (i / 2) + 1;
            string black = Session.MoveHistory[i].Position.ToCoordinateString();
            string? white = (i + 1 < Session.MoveHistory.Count) ? Session.MoveHistory[i + 1].Position.ToCoordinateString() : null;
            MoveTurns.Add(new MoveTurnRecord(turn, black, white));
        }

        // Status text
        if (GameStatus == GameStatus.InProgress && !IsThinking)
        {
            StatusText = $"{SideToMove} to move (Move {MoveCount + 1})";
        }

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanHoverMove));
        OnPropertyChanged(nameof(CanSwitchSides));
        OnPropertyChanged(nameof(BlackPiecesCount));
        OnPropertyChanged(nameof(WhitePiecesCount));
        OnPropertyChanged(nameof(IsBlackActiveTurn));
        OnPropertyChanged(nameof(IsWhiteActiveTurn));
        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(VariantBadgeText));
        OnPropertyChanged(nameof(IsStatusModified));
        OnPropertyChanged(nameof(IsGameSettingVisible));
        OnPropertyChanged(nameof(SettingsBadgeText));
        OnPropertyChanged(nameof(MoveCountText));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        SwitchSidesCommand.NotifyCanExecuteChanged();
    }

    private void HandleGameOver()
    {
        StopClock();
        if (GameStatus == GameStatus.BlackWon)
        {
            StatusText = "Black won!";
            bool isHumanWin = Settings.Mode == GameMode.HumanVsAi || Settings.Mode == GameMode.HumanVsHuman;
            _ = _soundService.PlaySoundAsync(isHumanWin ? SoundType.Win : SoundType.Loss);
            Analysis.SetGameOver("Mate!");
        }
        else if (GameStatus == GameStatus.WhiteWon)
        {
            StatusText = "White won!";
            bool isHumanWin = Settings.Mode == GameMode.AiVsHuman || Settings.Mode == GameMode.HumanVsHuman;
            _ = _soundService.PlaySoundAsync(isHumanWin ? SoundType.Win : SoundType.Loss);
            Analysis.SetGameOver("Mate!");
        }
        else if (GameStatus == GameStatus.Draw)
        {
            StatusText = "Game ended in a draw!";
            _ = _soundService.PlaySoundAsync(SoundType.Draw);
            Analysis.SetGameOver("Draw");
        }
    }

    private static string FormatMoveHistory(IReadOnlyList<Move> history)
    {
        if (history.Count == 0) return "No moves yet.";
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < history.Count; i += 2)
        {
            int turn = (i / 2) + 1;
            sb.Append($"{turn}. {history[i].Position.ToCoordinateString()}");
            if (i + 1 < history.Count)
            {
                sb.Append($" {history[i + 1].Position.ToCoordinateString()}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public async Task OpenFileAsync()
    {
        string? content = await _fileService.OpenGameFileAsync();
        if (string.IsNullOrWhiteSpace(content)) return;

        if (GameFileSerializer.TryParse(content, out var moves, out var size, out var variant, out _))
        {
            Settings.BoardSize = size;
            Settings.Variant = variant;
            ResetBoard(size, triggerAi: false);

            foreach (var m in moves)
            {
                Session.ApplyMove(m);
                _timeAtPly[Session.MoveHistory.Count] = (BlackRemainingTime, WhiteRemainingTime);
            }

            IsGameModified = false;
            _hasHumanContinuedOnTime = false;
            UpdateUiState();
            await _dialogService.ShowMessageAsync("Game Loaded", $"Loaded {moves.Count} moves ({variant}, {size}).");
            CheckAndTriggerAiMove();
        }
        else
        {
            await _dialogService.ShowMessageAsync("Load Failed", "The selected file is not a valid SGF or Gomoku record.");
        }
    }

    public async Task SaveFileAsync()
    {
        string sgf = SgfFormat.Serialize(
            Session.MoveHistory,
            Settings.BoardSize,
            Settings.Variant,
            blackPlayer: Settings.Mode is GameMode.HumanVsAi or GameMode.HumanVsHuman ? "Human" : "Computer",
            whitePlayer: Settings.Mode is GameMode.AiVsHuman or GameMode.HumanVsHuman ? "Human" : "Computer");

        string defaultName = $"Gomoku_{DateTime.Now:yyyyMMdd_HHmmss}.sgf";
        bool saved = await _fileService.SaveGameFileAsync(defaultName, sgf, "Smart Game Format (*.sgf)|*.sgf|Gomoku Record (*.gomoku)|*.gomoku");
        if (saved)
        {
            await _dialogService.ShowMessageAsync("Saved", "Game successfully saved.");
        }
    }

    public async Task OpenSettingsAsync()
    {
        BoardSize prevSize = Settings.BoardSize;
        GomokuVariant prevVariant = Settings.Variant;
        TimeControlMode prevTimeControl = Settings.TimeControl;
        int prevGameMinutes = Settings.GameTimeMinutes;
        AiEngineType prevEngine = Settings.AiEngine;
        int prevDepth = Settings.SearchDepth;
        int prevMoveSeconds = Settings.MoveTimeSeconds;

        var settingsVm = new SettingsViewModel(Settings);
        var result = await _dialogService.ShowCustomDialogAsync<bool?>(settingsVm);
        if (result == true)
        {
            bool rulesChanged = Settings.BoardSize != prevSize || Settings.Variant != prevVariant;
            if (rulesChanged && Session.MoveHistory.Count > 0 && !GameStatus.IsGameOver())
            {
                bool confirm = await _dialogService.ShowConfirmationAsync(
                    "Change Rules",
                    "Changing the board size or rules variant requires starting a new game.\n\nDo you want to start a new game now with the new rules?");
                if (!confirm)
                {
                    Settings.BoardSize = prevSize;
                    Settings.Variant = prevVariant;
                }
            }

            if (Session.MoveHistory.Count > 0 && !GameStatus.IsGameOver() && IsVsComputer)
            {
                if (Settings.AiEngine != prevEngine ||
                    Settings.TimeControl != prevTimeControl ||
                    Settings.GameTimeMinutes != prevGameMinutes ||
                    Settings.SearchDepth != prevDepth ||
                    Settings.MoveTimeSeconds != prevMoveSeconds)
                {
                    IsGameModified = true;
                }
            }

            ApplyUpdatedSettings(prevSize, prevVariant, prevTimeControl, prevGameMinutes);
        }
    }

    public void ApplyUpdatedSettings(BoardSize prevSize, GomokuVariant prevVariant, TimeControlMode prevTimeControl, int prevGameMinutes)
    {
        _soundService.IsMuted = !Settings.SoundEnabled;

        if (Settings.BoardSize != prevSize || Settings.Variant != prevVariant)
        {
            _engineHost.ClearTranspositionTable();
        }

        SavePreferences();

        if (Settings.BoardSize != prevSize || Settings.Variant != prevVariant ||
            Settings.BoardSize != Session.CurrentState.Size || Settings.Variant != Session.CurrentState.Variant)
        {
            ResetBoard(Settings.BoardSize);
        }
        else
        {
            if (Settings.TimeControl == TimeControlMode.TimePerGame &&
                (prevTimeControl != TimeControlMode.TimePerGame || prevGameMinutes != Settings.GameTimeMinutes))
            {
                TimeSpan initialClock = TimeSpan.FromMinutes(Math.Max(1, Settings.GameTimeMinutes));
                BlackRemainingTime = initialClock;
                WhiteRemainingTime = initialClock;
                StartClock();
            }
            else if (Settings.TimeControl != TimeControlMode.TimePerGame)
            {
                StopClock();
            }

            UpdateUiState();
            BoardLayoutChanged?.Invoke();
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SettingsBadgeText));
            OnPropertyChanged(nameof(VariantBadgeText));
            OnPropertyChanged(nameof(IsGameSettingVisible));
            OnPropertyChanged(nameof(IsStatusModified));
            CheckAndTriggerAiMove();
        }
    }

    public async Task OpenAboutAsync()
    {
        var about = new AboutInfo();
        await _dialogService.ShowCustomDialogAsync<bool?>(about);
    }
}

public sealed record MoveTurnRecord(int Turn, string BlackMove, string? WhiteMove)
{
    public string Notation => string.IsNullOrEmpty(WhiteMove) ? BlackMove : $"{BlackMove} {WhiteMove}";
}

