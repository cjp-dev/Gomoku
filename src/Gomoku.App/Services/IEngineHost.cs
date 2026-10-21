using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Services;

/// <summary>
/// Abstraction for running the Gomoku AI engine (in-process on desktop/tests, or in a Web Worker on the browser).
/// </summary>
public interface IEngineHost
{
    Task<(Move Move, SearchAnalysis? LastAnalysis)> ChooseMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        GameSettings settings,
        IProgress<SearchAnalysis>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken);

    void ClearTranspositionTable();
}

