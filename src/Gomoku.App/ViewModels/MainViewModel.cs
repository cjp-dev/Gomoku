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

    private CarbonAiPlayer _carbonPlayer;
    private SimpleAiPlayer _simplePlayer;
    private CancellationTokenSource? _aiCts;
    private CancellationTokenSource? _clockCts;
    private long _lastClockTickMs;

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

    public ObservableCollection<MoveTurnRecord> MoveTurns { get; } = [];

    public string BlackPlayerLabel => Settings.Mode switch
    {
        GameMode.AiVsHuman => "Computer",
        _ => "Human"
    };

    public string WhitePlayerLabel => Settings.Mode switch
    {
        GameMode.HumanVsAi or GameMode.AiVsAi => "Computer",
        _ => "Human"
    };

    public int BlackPiecesCount => (Session.MoveHistory.Count + 1) / 2;
    public int WhitePiecesCount => Session.MoveHistory.Count / 2;

    public string VariantBadgeText => Settings.Variant switch
    {
        GomokuVariant.Freestyle => "Freestyle",
        GomokuVariant.Exact5 => "Standard",
        GomokuVariant.Renju => "Renju",
        _ => Settings.Variant.ToString()
    };

    public bool IsClockVisible => Settings.TimeControl == TimeControlMode.TimePerGame;
    public string ClockText => SideToMove == PieceColor.Black ? BlackTimeFormatted : WhiteTimeFormatted;

    public bool IsHumanVsComputer => Settings.Mode == GameMode.HumanVsAi;
    public bool IsComputerVsHuman => Settings.Mode == GameMode.AiVsHuman;
    public bool IsHumanVsHuman => Settings.Mode == GameMode.HumanVsHuman;

    public bool IsSoundOn => Settings.SoundEnabled;
    public bool IsCoordinatesVisible => Settings.CoordinatesVisible;
    public bool IsGhostStoneOn => Settings.GhostStoneEnabled;

    public IRelayCommand NewGameCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    public IRelayCommand OpenFileCommand { get; }
    public IRelayCommand SaveFileCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenAboutCommand { get; }
    public IRelayCommand CancelSearchCommand { get; }
    public IRelayCommand ToggleAnalysisCommand { get; }
    public IRelayCommand ToggleSoundCommand { get; }
    public IRelayCommand ToggleCoordinatesCommand { get; }
    public IRelayCommand ToggleGhostStoneCommand { get; }

    public MainViewModel(
        IDialogService dialogService,
        IGameFileService fileService,
        ISoundService soundService,
        GameSettings? settings = null)
    {
        _dialogService = dialogService;
        _fileService = fileService;
        _soundService = soundService;
        Settings = settings ?? new GameSettings();
        _soundService.IsMuted = !Settings.SoundEnabled;

        Session = new GameSession(Settings.BoardSize, Settings.Variant);
        _carbonPlayer = new CarbonAiPlayer(new TranspositionTable(Settings.TranspositionTableEntries));
        _simplePlayer = new SimpleAiPlayer();

        NewGameCommand = new RelayCommand(NewGame);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAsync);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        CancelSearchCommand = new RelayCommand(CancelAiSearch);
        ToggleAnalysisCommand = new RelayCommand(() => IsAnalysisVisible = !IsAnalysisVisible);
        ToggleSoundCommand = new RelayCommand(() =>
        {
            Settings.SoundEnabled = !Settings.SoundEnabled;
            _soundService.IsMuted = !Settings.SoundEnabled;
            OnPropertyChanged(nameof(IsSoundOn));
        });
        ToggleCoordinatesCommand = new RelayCommand(() =>
        {
            Settings.CoordinatesVisible = !Settings.CoordinatesVisible;
            OnPropertyChanged(nameof(IsCoordinatesVisible));
            BoardLayoutChanged?.Invoke();
        });
        ToggleGhostStoneCommand = new RelayCommand(() =>
        {
            Settings.GhostStoneEnabled = !Settings.GhostStoneEnabled;
            OnPropertyChanged(nameof(IsGhostStoneOn));
        });

        ResetBoard(Settings.BoardSize);
    }

    public void SetGameMode(GameMode mode)
    {
        if (Settings.Mode != mode)
        {
            Settings.Mode = mode;
            NotifyModePropertiesChanged();
            CheckAndTriggerAiMove();
        }
    }

    private void NotifyModePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(IsComputerVsHuman));
        OnPropertyChanged(nameof(IsHumanVsHuman));
        OnPropertyChanged(nameof(BlackPlayerLabel));
        OnPropertyChanged(nameof(WhitePlayerLabel));
        OnPropertyChanged(nameof(CanHoverMove));
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
        UndoCommand?.NotifyCanExecuteChanged();
        RedoCommand?.NotifyCanExecuteChanged();
    }

    public void ResetBoard(BoardSize size, bool triggerAi = true)
    {
        CancelAiSearch();
        StopClock();
        Session.Reset(size, Settings.Variant, PieceColor.Black);

        TimeSpan initialClock = TimeSpan.FromMinutes(Math.Max(1, Settings.GameTimeMinutes));
        BlackRemainingTime = initialClock;
        WhiteRemainingTime = initialClock;

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
                    if (elapsedMs <= 0) continue;

                    if (SideToMove == PieceColor.Black)
                    {
                        TimeSpan next = BlackRemainingTime - TimeSpan.FromMilliseconds(elapsedMs);
                        if (next <= TimeSpan.Zero)
                        {
                            BlackRemainingTime = TimeSpan.Zero;
                            HandleTimeout(PieceColor.Black);
                            break;
                        }
                        BlackRemainingTime = next;
                    }
                    else if (SideToMove == PieceColor.White)
                    {
                        TimeSpan next = WhiteRemainingTime - TimeSpan.FromMilliseconds(elapsedMs);
                        if (next <= TimeSpan.Zero)
                        {
                            WhiteRemainingTime = TimeSpan.Zero;
                            HandleTimeout(PieceColor.White);
                            break;
                        }
                        WhiteRemainingTime = next;
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

    private void HandleTimeout(PieceColor timedOutPlayer)
    {
        CancelAiSearch();
        StopClock();
        GameStatus = timedOutPlayer == PieceColor.Black ? GameStatus.WhiteWon : GameStatus.BlackWon;
        StatusText = $"{timedOutPlayer} ran out of time! {timedOutPlayer.Opponent()} won!";
        OnPropertyChanged(nameof(CanHoverMove));
        _ = _soundService.PlaySoundAsync(SoundType.Loss);
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
        _ = _soundService.PlaySoundAsync(SoundType.StonePlaced);
        UpdateUiState();

        if (GameStatus.IsGameOver())
        {
            HandleGameOver();
        }
        else
        {
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

    private async Task RunAiTurnAsync()
    {
        if (IsThinking || GameStatus.IsGameOver()) return;

        IsThinking = true;
        StatusText = $"{Session.CurrentState.SideToMove} is thinking...";
        _aiCts = new CancellationTokenSource();

        try
        {
            var legalMoves = RuleEngine.Instance.GetLegalMoves(Session.CurrentState);
            if (legalMoves.Count == 0)
            {
                IsThinking = false;
                return;
            }

            IPlayer player = Settings.AiEngine == AiEngineType.Carbon ? _carbonPlayer : _simplePlayer;
            SearchLimits limits = CreateSearchLimits();

            var progress = new Progress<SearchAnalysis>(a => Analysis.Update(a));

            Move bestMove = await Task.Run(
                async () => await player.GetMoveAsync(Session.CurrentState, limits, legalMoves, progress, _aiCts.Token),
                _aiCts.Token);

            if (_aiCts == null || _aiCts.IsCancellationRequested || GameStatus.IsGameOver()) return;

            if (Session.ApplyMove(bestMove))
            {
                await _soundService.PlaySoundAsync(SoundType.StonePlaced);
                UpdateUiState();

                if (GameStatus.IsGameOver())
                {
                    HandleGameOver();
                }
                else if (Settings.Mode == GameMode.AiVsAi)
                {
                    await Task.Delay(200);
                    CheckAndTriggerAiMove();
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Search stopped";
        }
        catch (Exception ex)
        {
            StatusText = $"Engine error: {ex.Message}";
        }
        finally
        {
            IsThinking = false;
            UpdateUiState();
        }
    }

    public void CancelAiSearch()
    {
        if (_aiCts != null && !_aiCts.IsCancellationRequested)
        {
            _aiCts.Cancel();
            _aiCts.Dispose();
            _aiCts = null;
        }
        IsThinking = false;
    }

    public void NewGame()
    {
        ResetBoard(Settings.BoardSize);
    }

    public void Undo()
    {
        if (IsThinking) CancelAiSearch();

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

        UpdateUiState();
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
        OnPropertyChanged(nameof(BlackPiecesCount));
        OnPropertyChanged(nameof(WhitePiecesCount));
        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(VariantBadgeText));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void HandleGameOver()
    {
        StopClock();
        if (GameStatus == GameStatus.BlackWon)
        {
            StatusText = "Black won!";
            bool isHumanWin = Settings.Mode == GameMode.HumanVsAi || Settings.Mode == GameMode.HumanVsHuman;
            _ = _soundService.PlaySoundAsync(isHumanWin ? SoundType.Win : SoundType.Loss);
        }
        else if (GameStatus == GameStatus.WhiteWon)
        {
            StatusText = "White won!";
            bool isHumanWin = Settings.Mode == GameMode.AiVsHuman || Settings.Mode == GameMode.HumanVsHuman;
            _ = _soundService.PlaySoundAsync(isHumanWin ? SoundType.Win : SoundType.Loss);
        }
        else if (GameStatus == GameStatus.Draw)
        {
            StatusText = "Game ended in a draw!";
            _ = _soundService.PlaySoundAsync(SoundType.Draw);
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
            }

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
            blackPlayer: Settings.Mode is GameMode.HumanVsAi or GameMode.HumanVsHuman ? "Human" : "Carbon AI",
            whitePlayer: Settings.Mode is GameMode.AiVsHuman or GameMode.HumanVsHuman ? "Human" : "Carbon AI");

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

        var settingsVm = new SettingsViewModel(Settings);
        var result = await _dialogService.ShowCustomDialogAsync<bool?>(settingsVm);
        if (result == true)
        {
            ApplyUpdatedSettings(prevSize, prevVariant, prevTimeControl, prevGameMinutes);
        }
    }

    public void ApplyUpdatedSettings(BoardSize prevSize, GomokuVariant prevVariant, TimeControlMode prevTimeControl, int prevGameMinutes)
    {
        _soundService.IsMuted = !Settings.SoundEnabled;

        if (Settings.TranspositionTableEntries != _carbonPlayer.Table.Capacity)
        {
            _carbonPlayer = new CarbonAiPlayer(new TranspositionTable(Settings.TranspositionTableEntries));
        }

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

