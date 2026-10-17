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
        Depth = analysis.Depth > 0 ? analysis.Depth.ToString() : "-";

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
        Nodes = analysis.Nodes.ToString("N0");
        Speed = analysis.EvaluationsPerSec > 0 ? $"{analysis.EvaluationsPerSec:N0} nps" : "-";
        Time = $"{analysis.Elapsed.TotalSeconds:F2}s";
        Status = analysis.StatusMessage ?? "Searching...";

        Updated?.Invoke();
    }

    public void Reset()
    {
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

