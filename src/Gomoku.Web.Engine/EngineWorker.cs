using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Gomoku.Web.Worker;

namespace Gomoku.Web.Engine;

[SupportedOSPlatform("browser")]
public static partial class EngineWorker
{
    private static CarbonBitboardAiPlayer? s_carbonPlayer;
    private static readonly SimpleAiPlayer s_simplePlayer = new();
    private static GomokuVariant s_lastVariant = GomokuVariant.Freestyle;
    private static BoardSize s_lastSize = BoardSize.Size15x15;
    private static int s_lastThreads = 1;

    private static volatile int s_signal; // 0 = none, 1 = moveNow, 2 = cancel
    private static readonly ConcurrentQueue<string> s_progressQueue = new();

    [JSExport]
    public static void Init()
    {
        _ = RuleEngine.Instance;
        s_carbonPlayer ??= new CarbonBitboardAiPlayer(1, new LockFreeTranspositionTable(TranspositionTable.DefaultEntries));
    }

    [JSExport]
    public static void ClearTranspositionTable() => s_carbonPlayer?.Table.Clear();

    [JSExport]
    public static void Signal(int code)
    {
        s_signal = code;
    }

    [JSExport]
    public static string? DrainProgress()
    {
        return s_progressQueue.TryDequeue(out string? json) ? json : null;
    }

    [JSExport]
    public static async Task<string> ChooseMoveAsync(string requestJson)
    {
        s_signal = 0;
        while (s_progressQueue.TryDequeue(out _)) { }

        var (state, limits, engineType, ttEntries, mtEnabled, threadCount) = EngineProtocol.ReadRequest(requestJson);

        int threads = 1;
        if (mtEnabled)
        {
            threads = threadCount > 0
                ? Math.Clamp(threadCount, 1, 4)
                : Math.Clamp(Environment.ProcessorCount - 1, 1, 4);
        }

        if (s_carbonPlayer is null || s_carbonPlayer.Table.Capacity != ttEntries || s_lastThreads != threads)
        {
            s_carbonPlayer = new CarbonBitboardAiPlayer(threads, new LockFreeTranspositionTable(ttEntries));
        }
        else if (s_lastVariant != state.Variant || s_lastSize != state.Size)
        {
            s_carbonPlayer.Table.Clear();
        }

        s_lastVariant = state.Variant;
        s_lastSize = state.Size;
        s_lastThreads = threads;

        IReadOnlyList<Move> legalMoves = RuleEngine.Instance.GetLegalMoves(state);
        if (legalMoves.Count == 0)
        {
            return EngineProtocol.WriteResponse(new Move(0, 0, state.SideToMove), null);
        }

        using var cancelCts = new CancellationTokenSource();
        using var moveNowCts = new CancellationTokenSource();
        var poster = new ThreadSafeProgressPoster(cancelCts, moveNowCts);

        IPlayer ai = engineType == AiEngineType.Carbon ? s_carbonPlayer : s_simplePlayer;

        try
        {
            Move move = await Task.Run(() =>
                ai.GetMoveAsync(state, limits, legalMoves, poster, cancelCts.Token, moveNowCts.Token).AsTask());

            return EngineProtocol.WriteResponse(move, ai.LastAnalysis);
        }
        catch (OperationCanceledException)
        {
            Move fallback = ai.LastAnalysis?.BestMove ?? legalMoves[0];
            return EngineProtocol.WriteResponse(fallback, ai.LastAnalysis);
        }
    }

    private sealed class ThreadSafeProgressPoster(CancellationTokenSource cancelCts, CancellationTokenSource moveNowCts) : IProgress<SearchAnalysis>
    {
        private const long IntervalMs = 100;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastPostMs = -IntervalMs;
        private int _lastDepth = -1;
        private Move? _lastBest;

        public void Report(SearchAnalysis info)
        {
            int signal = s_signal;
            if (signal == 2)
            {
                cancelCts.Cancel();
                return;
            }
            if (signal == 1)
            {
                moveNowCts.Cancel();
                return;
            }

            long now = _clock.ElapsedMilliseconds;
            if (info.Depth == _lastDepth && info.BestMove == _lastBest && now - _lastPostMs < IntervalMs)
            {
                return;
            }

            _lastDepth = info.Depth;
            _lastBest = info.BestMove;
            _lastPostMs = now;

            s_progressQueue.Enqueue(EngineProtocol.WriteProgress(info));
        }
    }
}

