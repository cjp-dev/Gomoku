using FluentAssertions;
using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.App.Tests.Mocks;
using Gomoku.App.ViewModels;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.App.Tests;

public class MainViewModelTests
{
    private readonly MockDialogService _dialog = new();
    private readonly MockGameFileService _file = new();
    private readonly MockSoundService _sound = new();

    [Fact]
    public void ResetBoard_15x15_ShouldInitialize225Intersections()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.Intersections.Count.Should().Be(225);
        vm.SideToMove.Should().Be(PieceColor.Black);
        vm.GameStatus.Should().Be(GameStatus.InProgress);
        vm.Intersections.Count(i => i.IsStarPoint).Should().Be(5);
        _sound.PlayedSounds.Should().Contain(SoundType.GameStart);
    }

    [Fact]
    public void ResetBoard_19x19_ShouldInitialize361Intersections()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size19x19, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.Intersections.Count.Should().Be(361);
        vm.Intersections.Count(i => i.IsStarPoint).Should().Be(9);
    }

    [Fact]
    public void HumanMove_ShouldPlaceStoneAndAlternateSideToMove()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        var center = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        center.ClickCommand.Execute(null);

        center.Piece.Should().Be(PieceColor.Black);
        center.IsLastMove.Should().BeTrue();
        vm.SideToMove.Should().Be(PieceColor.White);
        vm.MoveCount.Should().Be(1);
        _sound.PlayedSounds.Should().Contain(SoundType.StonePlaced);
    }

    [Fact]
    public void IllegalMove_ShouldPlayIllegalMoveSound()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        var cell = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        cell.ClickCommand.Execute(null);

        // Click again on same cell
        cell.ClickCommand.Execute(null);

        _sound.PlayedSounds.Should().Contain(SoundType.IllegalMove);
    }

    [Fact]
    public void UndoAndRedo_ShouldRestorePreviousStates()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        var cell1 = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        var cell2 = vm.Intersections.First(i => i.X == 7 && i.Y == 8);

        cell1.ClickCommand.Execute(null);
        cell2.ClickCommand.Execute(null);

        vm.MoveCount.Should().Be(2);
        vm.CanUndo.Should().BeTrue();

        vm.UndoCommand.Execute(null);
        vm.MoveCount.Should().Be(1);
        cell2.Piece.Should().Be(PieceColor.Empty);
        vm.SideToMove.Should().Be(PieceColor.White);
        _sound.PlayedSounds.Should().Contain(SoundType.Undo);

        vm.CanRedo.Should().BeTrue();
        vm.RedoCommand.Execute(null);
        vm.MoveCount.Should().Be(2);
        cell2.Piece.Should().Be(PieceColor.White);
    }

    [Fact]
    public async Task OpenSettingsAsync_WhenSaved_ShouldPersistBoardSizeVariantSoundAndTimeControl()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman, SoundEnabled = true };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        _dialog.CustomDialogResult = true;
        _dialog.OnShowCustomDialog = obj =>
        {
            var svm = (SettingsViewModel)obj;
            svm.SelectedVariant = GomokuVariant.Renju;
            svm.SelectedTimeControl = Gomoku.Core.AI.TimeControlMode.FixedDepth;
            svm.SearchDepth = 8;
            svm.SoundEnabled = false;
            svm.SaveCommand.Execute(null);
        };

        await vm.OpenSettingsAsync();

        vm.Settings.BoardSize.Should().Be(BoardSize.Size15x15);
        vm.Settings.Variant.Should().Be(GomokuVariant.Renju);
        vm.Settings.TimeControl.Should().Be(Gomoku.Core.AI.TimeControlMode.FixedDepth);
        vm.Settings.SearchDepth.Should().Be(8);
        vm.Settings.SoundEnabled.Should().BeFalse();
        _sound.IsMuted.Should().BeTrue();
        vm.Intersections.Count.Should().Be(225);
    }

    [Fact]
    public void RenjuMode_ShouldPopulateIsForbiddenOnDoubleThreeIntersection()
    {
        GameSettings settings = new()
        {
            BoardSize = BoardSize.Size15x15,
            Variant = GomokuVariant.Renju,
            Mode = GameMode.HumanVsHuman
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        void Play(int x, int y) => vm.Intersections.First(i => i.X == x && i.Y == y).ClickCommand.Execute(null);

        Play(7, 5); // Black
        Play(0, 0); // White
        Play(7, 6); // Black
        Play(0, 1); // White
        Play(5, 7); // Black
        Play(0, 2); // White
        Play(6, 7); // Black
        Play(0, 3); // White

        // Now it is Black's turn and (7, 7) is a double-three (3x3) foul
        var center = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        center.IsForbidden.Should().BeTrue();
        center.ShowForbiddenMarker.Should().BeTrue();
    }

    [Fact]
    public void AnalysisViewModel_Update_ShouldFormatSearchingAndCompletedDepthsCorrectly()
    {
        AnalysisViewModel vm = new();
        bool updatedFired = false;
        vm.Updated += () => updatedFired = true;

        SearchAnalysis searchingAnalysis = new(
            MoveNumber: 5,
            Depth: 6,
            Score: 120,
            BestMove: new Move(7, 7, PieceColor.Black),
            Nodes: 15420,
            EvaluationsPerSec: 500000,
            Elapsed: TimeSpan.FromSeconds(0.45),
            StatusMessage: null,
            IsSearching: true);

        vm.Update(searchingAnalysis);

        vm.Depth.Should().Be("6 plies...");
        vm.Score.Should().Be("+120");
        vm.BestMove.Should().Be("H8");
        vm.Nodes.Should().Be("15,420");
        vm.Speed.Should().Be("500,000 nps");
        vm.Status.Should().Be("Searching...");
        updatedFired.Should().BeTrue();

        updatedFired = false;
        SearchAnalysis completedAnalysis = searchingAnalysis with
        {
            IsSearching = false
        };

        vm.Update(completedAnalysis);

        vm.Depth.Should().Be("6 plies");
        vm.Status.Should().Be("Ready");
        updatedFired.Should().BeTrue();
    }

    [Fact]
    public void AnalysisViewModel_OpeningBookAndCenterOpening_ShouldFormatDepthAsBookOrOpening()
    {
        AnalysisViewModel vm = new();

        SearchAnalysis bookAnalysis = new(
            MoveNumber: 2,
            Depth: 1,
            Score: 0,
            BestMove: new Move(8, 8, PieceColor.White),
            Nodes: 1,
            EvaluationsPerSec: 0,
            Elapsed: TimeSpan.FromMilliseconds(5),
            StatusMessage: "Opening book",
            IsSearching: false);

        vm.Update(bookAnalysis);
        vm.Depth.Should().Be("Book");
        vm.Status.Should().Be("Opening book");

        SearchAnalysis centerAnalysis = new(
            MoveNumber: 1,
            Depth: 1,
            Score: 0,
            BestMove: new Move(7, 7, PieceColor.Black),
            Nodes: 1,
            EvaluationsPerSec: 0,
            Elapsed: TimeSpan.FromMilliseconds(2),
            StatusMessage: "Center opening",
            IsSearching: false);

        vm.Update(centerAnalysis);
        vm.Depth.Should().Be("Opening");
        vm.Status.Should().Be("Center opening");
    }

    [Fact]
    public void AnalysisViewModel_MateScores_ShouldFormatAsMateInPlies()
    {
        AnalysisViewModel vm = new();

        SearchAnalysis winAnalysis = new(
            MoveNumber: 10,
            Depth: 8,
            Score: CarbonAiPlayer.WinMax - 3,
            BestMove: new Move(7, 7, PieceColor.Black),
            Nodes: 50000,
            EvaluationsPerSec: 100000,
            Elapsed: TimeSpan.FromSeconds(1),
            StatusMessage: null,
            IsSearching: false);

        vm.Update(winAnalysis);
        vm.Score.Should().Be("Mate in 3");

        SearchAnalysis lossAnalysis = winAnalysis with
        {
            Score = -CarbonAiPlayer.WinMax + 4
        };

        vm.Update(lossAnalysis);
        vm.Score.Should().Be("-Mate in 4");
    }

    [Fact]
    public void AnalysisViewModel_ConsumePlyAfterMove_ShouldDecrementMatePliesAndPreserveBestMove()
    {
        AnalysisViewModel vm = new();

        SearchAnalysis winAnalysis = new(
            MoveNumber: 22,
            Depth: 2,
            Score: CarbonAiPlayer.WinMax - 3,
            BestMove: new Move(6, 7, PieceColor.White), // G8
            Nodes: 1,
            EvaluationsPerSec: 0,
            Elapsed: TimeSpan.FromMilliseconds(5),
            StatusMessage: null,
            IsSearching: false);

        vm.Update(winAnalysis);
        vm.Score.Should().Be("Mate in 3");
        vm.BestMove.Should().Be("G8");

        // White plays G8: 1 ply consumed
        vm.ConsumePlyAfterMove();
        vm.Score.Should().Be("Mate in 2");
        vm.BestMove.Should().Be("G8"); // Best move preserved as indicator

        // Black plays defense: 1 ply consumed
        vm.ConsumePlyAfterMove();
        vm.Score.Should().Be("Mate in 1");
        vm.BestMove.Should().Be("G8");
    }

    [Fact]
    public void AnalysisViewModel_SetGameOver_ShouldSetMateAndPreserveBestMove()
    {
        AnalysisViewModel vm = new();

        SearchAnalysis winAnalysis = new(
            MoveNumber: 24,
            Depth: 2,
            Score: CarbonAiPlayer.WinMax - 1,
            BestMove: new Move(6, 4, PieceColor.White), // G5
            Nodes: 1,
            EvaluationsPerSec: 0,
            Elapsed: TimeSpan.FromMilliseconds(5),
            StatusMessage: null,
            IsSearching: false);

        vm.Update(winAnalysis);
        vm.Score.Should().Be("Mate in 1");
        vm.BestMove.Should().Be("G5");

        // Game ends with winning move G5
        vm.SetGameOver("Mate!");
        vm.Score.Should().Be("Mate!");
        vm.BestMove.Should().Be("G5"); // Best move preserved as indicator of winning move
        vm.Status.Should().Be("Game Over");
    }

    [Fact]
    public void MoveNowCommand_ShouldBeEnabledOnlyWhileThinking()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.IsThinking.Should().BeFalse();
        vm.MoveNowCommand.CanExecute(null).Should().BeFalse();

        vm.IsThinking = true;
        vm.MoveNowCommand.CanExecute(null).Should().BeTrue();

        vm.MoveNowCommand.Execute(null);

        vm.IsThinking = false;
        vm.MoveNowCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SettingsStore_ShouldLoadOnStartupAndPersistChanges()
    {
        AppPreferences saved = new(
            BoardWidth: 15,
            BoardHeight: 15,
            Variant: GomokuVariant.Renju,
            Mode: GameMode.HumanVsAi,
            AiEngine: AiEngineType.Carbon,
            TimeControl: TimeControlMode.TimePerMove,
            SearchDepth: 8,
            MoveTimeSeconds: 10,
            GameTimeMinutes: 10,
            SoundEnabled: false,
            ShowAnalysis: false);

        MockSettingsStore store = new(saved);
        MainViewModel vm = new(_dialog, _file, _sound, settingsStore: store);

        vm.Settings.BoardSize.Should().Be(BoardSize.Size15x15);
        vm.Settings.Variant.Should().Be(GomokuVariant.Renju);
        vm.IsSoundOn.Should().BeFalse();
        vm.IsAnalysisVisible.Should().BeFalse();

        // Toggle an option
        vm.ToggleSoundCommand.Execute(null);
        vm.IsSoundOn.Should().BeTrue();
        store.SavedHistory.Should().NotBeEmpty();
        store.Current.SoundEnabled.Should().BeTrue();

        // Change mode
        vm.SetGameMode(GameMode.AiVsHuman);
        store.Current.Mode.Should().Be(GameMode.AiVsHuman);
    }

    [Fact]
    public void IsGameModified_WhenUndoUsedInVsComputer_ShouldBecomeTrue()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsAi };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.IsGameModified.Should().BeFalse();
        vm.IsStatusModified.Should().BeFalse();

        var cell = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        cell.ClickCommand.Execute(null);

        vm.UndoCommand.Execute(null);

        vm.IsGameModified.Should().BeTrue();
        vm.IsStatusModified.Should().BeTrue();

        // New game should reset modified flag
        vm.NewGame();
        vm.IsGameModified.Should().BeFalse();
        vm.IsStatusModified.Should().BeFalse();
    }

    [Fact]
    public void SwitchSides_InHumanVsAi_ShouldSwapToAiVsHumanAndMarkModified()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsAi };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.CanSwitchSides.Should().BeTrue();
        vm.SwitchSidesCommand.Execute(null);

        vm.Settings.Mode.Should().Be(GameMode.AiVsHuman);
        vm.IsGameModified.Should().BeTrue();
        vm.IsStatusModified.Should().BeTrue();
    }

    [Fact]
    public void Undo_ShouldRefundClockTime()
    {
        GameSettings settings = new()
        {
            BoardSize = BoardSize.Size15x15,
            Mode = GameMode.HumanVsHuman,
            TimeControl = TimeControlMode.TimePerGame,
            GameTimeMinutes = 5
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        // Black takes 15s to play move 1
        vm.BlackRemainingTime = TimeSpan.FromSeconds(285);
        var cell1 = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        cell1.ClickCommand.Execute(null);

        // White takes 20s to play move 2
        vm.WhiteRemainingTime = TimeSpan.FromSeconds(280);
        var cell2 = vm.Intersections.First(i => i.X == 7 && i.Y == 8);
        cell2.ClickCommand.Execute(null);

        // During turn 3, Black spends 25s thinking (time drops to 260s)
        vm.BlackRemainingTime = TimeSpan.FromSeconds(260);

        // Undo move 2 (White's move): reverts to state at ply 1 where White was to move
        vm.UndoCommand.Execute(null);

        // White's remaining time at ply 1 was 300s (before White moved), and Black's was 285s
        vm.BlackRemainingTime.Should().Be(TimeSpan.FromSeconds(285));
        vm.WhiteRemainingTime.Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public async Task TimeoutContinuation_WhenAccepted_ShouldMarkGameModifiedAndKeepPlaying()
    {
        GameSettings settings = new()
        {
            BoardSize = BoardSize.Size15x15,
            Mode = GameMode.HumanVsAi,
            TimeControl = TimeControlMode.TimePerGame,
            GameTimeMinutes = 5
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings);
        _dialog.ConfirmationResult = true; // User clicks "Yes" to continue

        await vm.HandleTimeoutAsync(PieceColor.Black);

        vm.GameStatus.Should().Be(GameStatus.InProgress);
        vm.IsGameModified.Should().BeTrue();
        vm.IsStatusModified.Should().BeTrue();
        vm.BlackRemainingTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task TimeoutContinuation_ComputerCanStillLoseOnTime()
    {
        GameSettings settings = new()
        {
            BoardSize = BoardSize.Size15x15,
            Mode = GameMode.HumanVsAi,
            TimeControl = TimeControlMode.TimePerGame,
            GameTimeMinutes = 5
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings, new ScriptedEngineHost(instantReplies: 1));
        _dialog.ConfirmationResult = true; // Continue after the human's flag falls
        _dialog.ConfirmationDelay = TimeSpan.FromMilliseconds(300); // Answered later, as in the browser

        vm.Intersections.First(i => i.X == 7 && i.Y == 7).ClickCommand.Execute(null);
        (await WaitUntilAsync(() => vm.MoveCount == 2 && !vm.IsThinking)).Should().BeTrue("the computer replies instantly");

        // The human's flag falls via the clock loop, and the human chooses to continue.
        vm.BlackRemainingTime = TimeSpan.FromMilliseconds(100);
        (await WaitUntilAsync(() => vm.IsGameModified)).Should().BeTrue("the timeout dialog was answered with Yes");

        // Now the computer thinks forever; its clock must still run out.
        vm.Intersections.First(i => i.Piece == PieceColor.Empty).ClickCommand.Execute(null);
        (await WaitUntilAsync(() => vm.IsThinking)).Should().BeTrue();
        vm.WhiteRemainingTime = TimeSpan.FromMilliseconds(100);

        (await WaitUntilAsync(() => vm.GameStatus == GameStatus.BlackWon)).Should().BeTrue("the computer ran out of time");
    }

    [Fact]
    public async Task Clocks_DoNotTickBeforeTheFirstMove()
    {
        GameSettings settings = new()
        {
            BoardSize = BoardSize.Size15x15,
            Mode = GameMode.HumanVsHuman,
            TimeControl = TimeControlMode.TimePerGame,
            GameTimeMinutes = 5
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        await Task.Delay(700);

        vm.BlackRemainingTime.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(GameMode.HumanVsAi, "Human", "Computer")]
    [InlineData(GameMode.AiVsHuman, "Computer", "Human")]
    [InlineData(GameMode.HumanVsHuman, "Player 1", "Player 2")]
    public void PlayerLabels_FollowTheGameMode(GameMode mode, string black, string white)
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = mode };
        MainViewModel vm = new(_dialog, _file, _sound, settings, new ScriptedEngineHost(instantReplies: 0));

        vm.BlackPlayerLabel.Should().Be(black);
        vm.WhitePlayerLabel.Should().Be(white);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(25);
        }
        return condition();
    }

    [Fact]
    public async Task NewGameSafeguard_WhenGameInProgressAndCancelled_ShouldNotReset()
    {
        GameSettings settings = new() { BoardSize = BoardSize.Size15x15, Mode = GameMode.HumanVsHuman };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        var cell = vm.Intersections.First(i => i.X == 7 && i.Y == 7);
        cell.ClickCommand.Execute(null);

        vm.MoveCount.Should().Be(1);

        _dialog.ConfirmationResult = false; // User cancels abandon prompt
        await vm.NewGameAsync();

        vm.MoveCount.Should().Be(1); // Not reset!

        _dialog.ConfirmationResult = true; // User confirms abandon prompt
        await vm.NewGameAsync();

        vm.MoveCount.Should().Be(0); // Reset!
    }

    [Fact]
    public void Badges_ShouldShowCorrectTextAndVisibility()
    {
        GameSettings settings = new()
        {
            Variant = GomokuVariant.Renju,
            Mode = GameMode.HumanVsAi,
            AiEngine = AiEngineType.Carbon,
            TimeControl = TimeControlMode.TimePerMove,
            MoveTimeSeconds = 5
        };
        MainViewModel vm = new(_dialog, _file, _sound, settings);

        vm.VariantBadgeText.Should().Be("Renju");
        vm.IsGameSettingVisible.Should().BeTrue();
        vm.SettingsBadgeText.Should().Be("5s / move");

        // When vs Human, setting badge should be hidden
        vm.SetGameMode(GameMode.HumanVsHuman);
        vm.IsGameSettingVisible.Should().BeFalse();
    }

    [Fact]
    public void AnalysisViewModel_TimeFormat_ShouldMatchCheckersFormat()
    {
        AnalysisViewModel vm = new();
        SearchAnalysis analysis = new(
            MoveNumber: 1,
            Depth: 6,
            Score: 10,
            BestMove: null,
            Nodes: 1000,
            EvaluationsPerSec: 5000,
            Elapsed: TimeSpan.FromSeconds(1.93),
            StatusMessage: null,
            IsSearching: false);

        vm.Update(analysis);
        vm.Time.Should().Be("0:01.9");

        SearchAnalysis analysisZero = new(
            MoveNumber: 1,
            Depth: 1,
            Score: 0,
            BestMove: null,
            Nodes: 1,
            EvaluationsPerSec: 0,
            Elapsed: TimeSpan.Zero,
            StatusMessage: null,
            IsSearching: false);

        vm.Update(analysisZero);
        vm.Time.Should().Be("0:00.0");
    }

    [Fact]
    public async Task LocalEngineHost_ChooseMoveAsync_ShouldWorkWithSingleAndMultiThreading()
    {
        LocalEngineHost host = new();
        BoardState state = new(BoardSize.Size15x15, GomokuVariant.Freestyle);
        var rules = RuleEngine.Instance;
        state = rules.ApplyMove(state, new Move(7, 7, PieceColor.Black));
        var legal = rules.GetLegalMoves(state);
        SearchLimits limits = SearchLimits.FixedDepth(2, useOpeningBook: false);

        // 1. Multi-threaded
        GameSettings mtSettings = new() { MultiThreadingEnabled = true, ThreadCount = 2 };
        var (moveMt, _) = await host.ChooseMoveAsync(state, limits, legal, mtSettings, null, CancellationToken.None, CancellationToken.None);
        legal.Should().Contain(moveMt);

        // 2. Single-threaded
        GameSettings stSettings = new() { MultiThreadingEnabled = false, ThreadCount = 1 };
        var (moveSt, _) = await host.ChooseMoveAsync(state, limits, legal, stSettings, null, CancellationToken.None, CancellationToken.None);
        legal.Should().Contain(moveSt);
    }
}
