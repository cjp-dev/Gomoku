using FluentAssertions;
using Gomoku.App.Models;
using Gomoku.App.Tests.Mocks;
using Gomoku.App.ViewModels;
using Gomoku.Core.AI;
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
            svm.SelectedBoardSize = BoardSize.Size20x20;
            svm.SelectedVariant = GomokuVariant.Renju;
            svm.SelectedTimeControl = Gomoku.Core.AI.TimeControlMode.FixedDepth;
            svm.SearchDepth = 8;
            svm.SoundEnabled = false;
            svm.SaveCommand.Execute(null);
        };

        await vm.OpenSettingsAsync();

        vm.Settings.BoardSize.Should().Be(BoardSize.Size20x20);
        vm.Settings.Variant.Should().Be(GomokuVariant.Renju);
        vm.Settings.TimeControl.Should().Be(Gomoku.Core.AI.TimeControlMode.FixedDepth);
        vm.Settings.SearchDepth.Should().Be(8);
        vm.Settings.SoundEnabled.Should().BeFalse();
        _sound.IsMuted.Should().BeTrue();
        vm.Intersections.Count.Should().Be(400);
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
}
