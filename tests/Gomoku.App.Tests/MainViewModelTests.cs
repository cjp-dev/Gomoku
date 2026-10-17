using FluentAssertions;
using Gomoku.App.Models;
using Gomoku.App.Tests.Mocks;
using Gomoku.App.ViewModels;
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
}
