using FluentAssertions;
using Gomoku.App.Models;
using Gomoku.App.ViewModels;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.App.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void Settings_DefaultValues_ShouldMatchStandard()
    {
        GameSettings settings = new();
        SettingsViewModel vm = new(settings);

        vm.SelectedBoardSize.Should().Be(BoardSize.Size15x15);
        vm.SelectedVariant.Should().Be(GomokuVariant.Freestyle);
        vm.SelectedMode.Should().Be(GameMode.HumanVsAi);
        vm.SelectedAiEngine.Should().Be(AiEngineType.Carbon);
        vm.SearchDepth.Should().Be(6);
        vm.UseOpeningBook.Should().BeTrue();
    }

    [Fact]
    public void SaveCommand_ShouldUpdateOriginalSettings()
    {
        GameSettings settings = new();
        SettingsViewModel vm = new(settings);

        vm.SelectedBoardSize = BoardSize.Size19x19;
        vm.SelectedVariant = GomokuVariant.Renju;
        vm.SelectedMode = GameMode.HumanVsHuman;
        vm.SearchDepth = 10;
        vm.SaveCommand.Execute(null);

        settings.BoardSize.Should().Be(BoardSize.Size19x19);
        settings.Variant.Should().Be(GomokuVariant.Renju);
        settings.Mode.Should().Be(GameMode.HumanVsHuman);
        settings.SearchDepth.Should().Be(10);
    }

    [Fact]
    public void ResetDefaultsCommand_ShouldRestoreDefaultValues()
    {
        GameSettings settings = new();
        SettingsViewModel vm = new(settings);

        vm.SelectedBoardSize = BoardSize.Size20x20;
        vm.SearchDepth = 12;

        vm.ResetDefaultsCommand.Execute(null);

        vm.SelectedBoardSize.Should().Be(BoardSize.Size15x15);
        vm.SearchDepth.Should().Be(6);
    }
}
