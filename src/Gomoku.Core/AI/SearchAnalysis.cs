using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Telemetry emitted by the AI search engine in real time.
/// </summary>
/// <param name="CurrentMove">The root move the search is examining right now; null when not searching.</param>
public sealed record SearchAnalysis(
    int MoveNumber,
    int Depth,
    int Score,
    Move? BestMove,
    long Nodes,
    long EvaluationsPerSec,
    TimeSpan Elapsed,
    string? StatusMessage = null,
    bool IsSearching = false,
    Move? CurrentMove = null);
