using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.App.Services;

/// <summary>
/// Runs the Gomoku AI engine on a thread-pool thread in the current process (for WPF, CLI, and unit tests).
/// </summary>
public sealed class LocalEngineHost : IEngineHost
{
    private CarbonBitboardAiPlayer _carbonPlayer;
    private readonly SimpleAiPlayer _simplePlayer = new();
    private GomokuVariant _lastVariant = GomokuVariant.Freestyle;
    private BoardSize _lastSize = BoardSize.Size15x15;
    private int _lastThreads = 0;

    public Func<GameSettings, IPlayer>? PlayerFactory { get; set; }

    public LocalEngineHost(int defaultTtEntries = TranspositionTable.DefaultEntries)
    {
        _carbonPlayer = new CarbonBitboardAiPlayer(0, new LockFreeTranspositionTable(defaultTtEntries));
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
        int targetThreads = settings.MultiThreadingEnabled
            ? (settings.ThreadCount > 0 ? settings.ThreadCount : Environment.ProcessorCount)
            : 1;

        if (_carbonPlayer.Table.Capacity != settings.TranspositionTableEntries || _lastThreads != targetThreads)
        {
            _carbonPlayer = new CarbonBitboardAiPlayer(targetThreads, new LockFreeTranspositionTable(settings.TranspositionTableEntries));
            _lastThreads = targetThreads;
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

