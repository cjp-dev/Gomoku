using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Gomoku.Web.Worker;

namespace Gomoku.Web.Engine;

[SupportedOSPlatform("browser")]
public static partial class EngineWorker
{
    private const int DefaultAutoThreads = 4;
    private const int MaxManualThreads = 8;

    private static readonly SemaphoreSlim s_searchLock = new(1, 1);
    private static CarbonBitboardAiPlayer? s_carbonPlayer;
    private static readonly SimpleAiPlayer s_simplePlayer = new();
    private static GomokuVariant s_lastVariant = GomokuVariant.Freestyle;
    private static BoardSize s_lastSize = BoardSize.Size15x15;
    private static int s_lastThreads = 1;

    private static volatile int s_signal; // 0 = none, 1 = moveNow, 2 = cancel
    private static volatile CancellationTokenSource? s_activeCancelCts;
    private static volatile CancellationTokenSource? s_activeMoveNowCts;
    private static readonly ConcurrentQueue<string> s_progressQueue = new();

    [JSExport]
    public static Task InitAsync()
    {
        ThreadPool.GetMinThreads(out int minWorkers, out int minIo);
        if (minWorkers < 12)
        {
            ThreadPool.SetMinThreads(12, Math.Max(minIo, 4));
        }

        _ = RuleEngine.Instance;
        s_carbonPlayer ??= new CarbonBitboardAiPlayer(1, new LockFreeTranspositionTable(TranspositionTable.DefaultEntries));
        return Task.CompletedTask;
    }

    [JSExport]
    public static Task ClearTranspositionTableAsync()
    {
        s_carbonPlayer?.Table.Clear();
        return Task.CompletedTask;
    }

    [JSExport]
    public static Task SignalAsync(int code)
    {
        s_signal = code;
        try
        {
            if (code == 2)
            {
                s_activeCancelCts?.Cancel();
            }
            else if (code == 1)
            {
                s_activeMoveNowCts?.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Search already finished and disposed its token source.
        }
        return Task.CompletedTask;
    }

    [JSExport]
    public static Task<string?> DrainProgressAsync()
    {
        if (!s_progressQueue.TryDequeue(out string? first))
        {
            return Task.FromResult<string?>(null);
        }

        if (!s_progressQueue.TryPeek(out _))
        {
            return Task.FromResult<string?>(first);
        }

        var sb = new StringBuilder(first);
        while (s_progressQueue.TryDequeue(out string? next))
        {
            sb.Append('\n').Append(next);
        }
        return Task.FromResult<string?>(sb.ToString());
    }

    [JSExport]
    public static async Task<string> ChooseMoveAsync(string requestJson)
    {
        // Cancel any lingering previous search before acquiring the exclusive search lock.
        try { s_activeCancelCts?.Cancel(); } catch (ObjectDisposedException) { }

        await s_searchLock.WaitAsync();
        try
        {
            s_signal = 0;
            while (s_progressQueue.TryDequeue(out _)) { }

            var (state, limits, engineType, ttEntries, mtEnabled, threadCount) = EngineProtocol.ReadRequest(requestJson);

            int threads = 1;
            if (mtEnabled)
            {
                threads = threadCount > 0
                    ? Math.Clamp(threadCount, 1, MaxManualThreads)
                    : Math.Clamp(Environment.ProcessorCount, 1, DefaultAutoThreads);
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
            s_activeCancelCts = cancelCts;
            s_activeMoveNowCts = moveNowCts;

            // Re-check in case SignalAsync arrived right as tokens were armed.
            int earlySignal = s_signal;
            if (earlySignal == 2) cancelCts.Cancel();
            else if (earlySignal == 1) moveNowCts.Cancel();

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
            finally
            {
                s_activeCancelCts = null;
                s_activeMoveNowCts = null;
            }
        }
        finally
        {
            s_searchLock.Release();
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
                try { cancelCts.Cancel(); } catch (ObjectDisposedException) { }
                return;
            }
            if (signal == 1)
            {
                try { moveNowCts.Cancel(); } catch (ObjectDisposedException) { }
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
