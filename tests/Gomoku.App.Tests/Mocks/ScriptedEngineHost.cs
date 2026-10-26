using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Tests.Mocks;

/// <summary>
/// Answers the first <c>instantReplies</c> searches immediately with the first legal move,
/// then "thinks" until the search is cancelled, so the computer's clock can run out.
/// </summary>
public sealed class ScriptedEngineHost(int instantReplies) : IEngineHost
{
    private int _calls;

    public async Task<(Move Move, SearchAnalysis? LastAnalysis)> ChooseMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        GameSettings settings,
        IProgress<SearchAnalysis>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        if (Interlocked.Increment(ref _calls) > instantReplies)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return (legalMoves[0], null);
    }

    public void ClearTranspositionTable()
    {
    }
}
