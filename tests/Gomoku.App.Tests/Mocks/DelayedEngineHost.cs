using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Tests.Mocks;

/// <summary>
/// Answers with the first legal move after <c>delayMs</c>, so a search can be made slow or instant.
/// </summary>
public sealed class DelayedEngineHost(int delayMs) : IEngineHost
{
    public async Task<(Move Move, SearchAnalysis? LastAnalysis)> ChooseMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        GameSettings settings,
        IProgress<SearchAnalysis>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        await Task.Delay(delayMs, cancellationToken);
        return (legalMoves[0], null);
    }

    public void ClearTranspositionTable()
    {
    }
}
