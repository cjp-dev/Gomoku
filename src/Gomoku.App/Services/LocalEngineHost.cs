using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Services;

/// <summary>
/// Runs the Gomoku AI engine on a thread-pool thread in the current process (for WPF, CLI, and unit tests).
/// </summary>
public sealed class LocalEngineHost : IEngineHost
{
    private CarbonAiPlayer _carbonPlayer;
    private readonly SimpleAiPlayer _simplePlayer = new();
    private GomokuVariant _lastVariant = GomokuVariant.Freestyle;
    private BoardSize _lastSize = BoardSize.Default;

    public Func<GameSettings, IPlayer>? PlayerFactory { get; set; }

    public LocalEngineHost(int defaultTtEntries = TranspositionTable.DefaultEntries)
    {
        _carbonPlayer = new CarbonAiPlayer(new TranspositionTable(defaultTtEntries));
    }

    public async Task<(Move Move, SearchAnalysis? LastAnalysis)> ChooseMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        GameSettings settings,
        IProgress<SearchAnalysis>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        if (_carbonPlayer.Table.Capacity != settings.TranspositionTableEntries)
        {
            _carbonPlayer = new CarbonAiPlayer(new TranspositionTable(settings.TranspositionTableEntries));
        }
        else if (_lastVariant != settings.Variant || _lastSize != settings.BoardSize)
        {
            _carbonPlayer.Table.Clear();
        }

        _lastVariant = settings.Variant;
        _lastSize = settings.BoardSize;

        IPlayer ai = PlayerFactory?.Invoke(settings) ??
                     (settings.AiEngine == AiEngineType.Carbon ? _carbonPlayer : _simplePlayer);

        Move move = await Task.Run(
            async () => await ai.GetMoveAsync(state, limits, legalMoves, progress, cancellationToken, moveNowToken),
            cancellationToken);

        return (move, ai.LastAnalysis);
    }

    public void ClearTranspositionTable() => _carbonPlayer.Table.Clear();
}

