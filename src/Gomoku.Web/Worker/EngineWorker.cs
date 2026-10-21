using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Gomoku.App.Models;
using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Web.Worker;

[SupportedOSPlatform("browser")]
public static partial class EngineWorker
{
    private static CarbonAiPlayer? s_carbonPlayer;
    private static readonly SimpleAiPlayer s_simplePlayer = new();
    private static GomokuVariant s_lastVariant = GomokuVariant.Freestyle;
    private static BoardSize s_lastSize = BoardSize.Default;

    [JSExport]
    public static void Init()
    {
        _ = RuleEngine.Instance;
        s_carbonPlayer ??= new CarbonAiPlayer(new TranspositionTable(TranspositionTable.DefaultEntries));
    }

    [JSExport]
    public static void ClearTranspositionTable() => s_carbonPlayer?.Table.Clear();

    [JSExport]
    public static string ChooseMove(string requestJson)
    {
        var (state, limits, engineType, ttEntries) = EngineProtocol.ReadRequest(requestJson);

        if (s_carbonPlayer is null || s_carbonPlayer.Table.Capacity != ttEntries)
        {
            s_carbonPlayer = new CarbonAiPlayer(new TranspositionTable(ttEntries));
        }
        else if (s_lastVariant != state.Variant || s_lastSize != state.Size)
        {
            s_carbonPlayer.Table.Clear();
        }
        s_lastVariant = state.Variant;
        s_lastSize = state.Size;

        IReadOnlyList<Move> legalMoves = RuleEngine.Instance.GetLegalMoves(state);
        if (legalMoves.Count == 0)
        {
            return EngineProtocol.WriteResponse(new Move(0, 0, state.SideToMove), null);
        }

        using var cancelCts = new CancellationTokenSource();
        using var moveNowCts = new CancellationTokenSource();
        var poster = new ProgressPoster(cancelCts, moveNowCts);

        IPlayer ai = engineType == AiEngineType.Carbon ? s_carbonPlayer : s_simplePlayer;

        try
        {
            Move move = ai.GetMoveAsync(state, limits, legalMoves, poster, cancelCts.Token, moveNowCts.Token)
                .GetAwaiter().GetResult();

            return EngineProtocol.WriteResponse(move, ai.LastAnalysis);
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancellation via SharedArrayBuffer signal (code = 2):
            // return cleanly so the worker stays alive and preserves its Transposition Table.
            Move fallback = ai.LastAnalysis?.BestMove ?? legalMoves[0];
            return EngineProtocol.WriteResponse(fallback, ai.LastAnalysis);
        }
    }

    [JSImport("postProgress", "engine-worker")]
    private static partial void PostProgress(string json);

    [JSImport("pollSignal", "engine-worker")]
    private static partial int PollSignal();

    private sealed class ProgressPoster(CancellationTokenSource cancelCts, CancellationTokenSource moveNowCts) : IProgress<SearchAnalysis>
    {
        private const long IntervalMs = 100;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastPostMs = -IntervalMs;
        private int _lastDepth = -1;
        private Move? _lastBest;

        public void Report(SearchAnalysis info)
        {
            int signal = PollSignal();
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
            PostProgress(EngineProtocol.WriteProgress(info));
        }
    }
}

