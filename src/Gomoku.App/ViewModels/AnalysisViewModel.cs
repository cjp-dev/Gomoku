using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Gomoku.Core.AI;

namespace Gomoku.App.ViewModels;

/// <summary>
/// ViewModel presenting real-time AI search analysis, depth, evaluation, and nodes per second.
/// </summary>
public sealed partial class AnalysisViewModel : ObservableObject
{
    public event Action? Updated;

    [ObservableProperty]
    private string _move = "-";

    [ObservableProperty]
    private string _depth = "-";

    [ObservableProperty]
    private string _score = "-";

    [ObservableProperty]
    private string _bestMove = "-";

    [ObservableProperty]
    private string _nodes = "-";

    [ObservableProperty]
    private string _speed = "-";

    [ObservableProperty]
    private string _time = "-";

    [ObservableProperty]
    private string _status = "Ready";

    public void Update(SearchAnalysis analysis)
    {
        Move = analysis.MoveNumber > 0 ? analysis.MoveNumber.ToString(CultureInfo.InvariantCulture) : "-";

        if (analysis.StatusMessage is "Opening book" or "Center opening")
        {
            Depth = analysis.StatusMessage == "Opening book" ? "Book" : "Opening";
        }
        else if (analysis.Depth > 0)
        {
            Depth = analysis.IsSearching ? $"{analysis.Depth} plies..." : $"{analysis.Depth} plies";
        }
        else
        {
            Depth = "-";
        }

        if (analysis.Score >= CarbonAiPlayer.WinMin)
        {
            int plies = CarbonAiPlayer.WinMax - analysis.Score;
            Score = $"Mate in {plies}";
        }
        else if (analysis.Score <= -CarbonAiPlayer.WinMin)
        {
            int plies = analysis.Score - (-CarbonAiPlayer.WinMax);
            Score = $"-Mate in {plies}";
        }
        else
        {
            Score = analysis.Score.ToString("+0;-0;0");
        }

        BestMove = analysis.BestMove.HasValue ? analysis.BestMove.Value.Position.ToCoordinateString() : "-";
        Nodes = analysis.Nodes.ToString("N0", CultureInfo.InvariantCulture);
        Speed = analysis.EvaluationsPerSec > 0 ? $"{analysis.EvaluationsPerSec.ToString("N0", CultureInfo.InvariantCulture)} nps" : "-";
        Time = $"{analysis.Elapsed.TotalSeconds:F2}s";
        Status = analysis.StatusMessage ?? (analysis.IsSearching ? "Searching..." : "Ready");

        Updated?.Invoke();
    }

    public void SetGameOver(string result)
    {
        Score = result;
        Status = "Game Over";
        Updated?.Invoke();
    }

    public void ConsumePlyAfterMove()
    {
        if (Score.StartsWith("Mate in ", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(Score["Mate in ".Length..], out int plies))
            {
                int remaining = plies - 1;
                Score = remaining <= 0 ? "Mate!" : $"Mate in {remaining}";
            }
        }
        else if (Score.StartsWith("-Mate in ", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(Score["-Mate in ".Length..], out int plies))
            {
                int remaining = plies - 1;
                Score = remaining <= 0 ? "Mate!" : $"-Mate in {remaining}";
            }
        }

        Status = "Ready";
        Updated?.Invoke();
    }

    public void Reset()
    {
        Move = "-";
        Depth = "-";
        Score = "-";
        BestMove = "-";
        Nodes = "-";
        Speed = "-";
        Time = "-";
        Status = "Ready";
        Updated?.Invoke();
    }
}

