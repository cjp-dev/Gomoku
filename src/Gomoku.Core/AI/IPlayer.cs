using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Common abstraction for a Gomoku player (Human, Simple AI, Carbon AI, etc.).
/// </summary>
public interface IPlayer
{
    string Name { get; }

    SearchAnalysis? LastAnalysis => null;

    ValueTask<Move> GetMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        IProgress<SearchAnalysis>? progress = null,
        CancellationToken cancellationToken = default);
}

