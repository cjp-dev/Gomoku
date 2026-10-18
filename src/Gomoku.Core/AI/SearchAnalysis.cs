using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Telemetry emitted by the AI search engine in real time.
/// </summary>
public sealed record SearchAnalysis(
    int MoveNumber,
    int Depth,
    int Score,
    Move? BestMove,
    long Nodes,
    long EvaluationsPerSec,
    TimeSpan Elapsed,
    string? StatusMessage = null,
    bool IsSearching = false);
