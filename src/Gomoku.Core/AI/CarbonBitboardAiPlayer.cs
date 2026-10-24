using System.Diagnostics;
using System.Numerics;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// "New Carbon AI Bitboard" experimental engine copy.
/// Features 1D contiguous memory layout, bitboard Chebyshev dilation candidate generation
/// (eliminating 24 adjacency counter writes per node), Lazy SMP multithreading, and 16-byte lock-free TT,
/// while keeping 100% of Carbon's directional ray pattern evaluation and QuickWinSearch VCF solver.
/// </summary>
public sealed class CarbonBitboardAiPlayer : IPlayer
{
    public const int WinMin = 25000;
    public const int WinMax = 30000;
    public const int Inf = 32000;
    public const int MaxCand = 256;
    public const int MatchSpare = 7;
    public const int TimeoutPrevent = 5;

    public string Name => ThreadCount > 1 ? $"Carbon AI ({ThreadCount}T)" : "Carbon AI";
    public SearchAnalysis? LastAnalysis { get; private set; }

    public int ThreadCount { get; }
    public LockFreeTranspositionTable Table { get; }

    private const int WorkerCounterStride = 8; // 64-byte cache-line spacing (8 x Int64)

    private readonly BitboardPatternBoard _pb = new();
    private long _totalNodes;
    private long[]? _activeWorkerNodes;
    private int _activeNumWorkers;
    private long _startTime;
    private long _stopTime;
    private int _timeoutTurnMs = 1000000;
    private volatile bool _terminate;

    private IProgress<SearchAnalysis>? _activeProgress;
    private Stopwatch? _activeSw;
    private long _lastReportMs;
    private long _lastYieldMs;
    private int _currentDepth;
    private int _currentMoveNumber;
    private PieceColor _currentSideToMove;
    private Point _activeBestMovePoint;
    private int _activeBestScore;

    public struct Cand
    {
        public int X, Y;
        public int Value;
        public Cand(int x, int y, int v) { X = x; Y = y; Value = v; }
    }

    public CarbonBitboardAiPlayer() : this(0, (LockFreeTranspositionTable?)null) { }

    public CarbonBitboardAiPlayer(int threadCount) : this(threadCount, (LockFreeTranspositionTable?)null) { }

    public CarbonBitboardAiPlayer(LockFreeTranspositionTable? table) : this(0, table) { }

    public CarbonBitboardAiPlayer(int threadCount, LockFreeTranspositionTable? table)
    {
        ThreadCount = threadCount > 0
            ? threadCount
            : Math.Max(1, Math.Min(16, Environment.ProcessorCount));
        Table = table ?? new LockFreeTranspositionTable(LockFreeTranspositionTable.DefaultCapacity);
    }

    public ValueTask<Move> GetMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        IProgress<SearchAnalysis>? progress = null,
        CancellationToken cancellationToken = default) =>
        GetMoveAsync(state, limits, legalMoves, progress, cancellationToken, moveNowToken: default);

    public ValueTask<Move> GetMoveAsync(
        BoardState state,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        IProgress<SearchAnalysis>? progress,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (legalMoves.Count == 0)
        {
            throw new InvalidOperationException("No legal moves available.");
        }

        Stopwatch sw = Stopwatch.StartNew();
        _startTime = Environment.TickCount64;
        _terminate = false;
        _totalNodes = 0;

        // 1. Center opening
        if (state.MoveCount == 0)
        {
            Point center = state.Size.Center;
            Move centerMove = new(center.X, center.Y, state.SideToMove);
            LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, 0, centerMove, 1, 0, sw.Elapsed, "Center opening", IsSearching: false);
            progress?.Report(LastAnalysis);
            return ValueTask.FromResult(centerMove);
        }

        // Initialize pattern board
        _pb.Start(state.Size.Width, state.Size.Height, state.Variant, state.FirstPlayer);
        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                PieceColor p = state.GetCell(x, y);
                if (p is PieceColor.Black or PieceColor.White)
                {
                    _pb.Who = p;
                    _pb.MakeMove(x, y);
                }
            }
        }
        _pb.Who = state.SideToMove;

        // 2. Opening book check
        if (limits.UseOpeningBook && OpeningBook.TryGetMove(_pb, out Point bookPoint))
        {
            Move bookMove = new(bookPoint.X, bookPoint.Y, state.SideToMove);
            if (legalMoves.Contains(bookMove))
            {
                LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, 0, bookMove, 1, 0, sw.Elapsed, "Opening book", IsSearching: false);
                progress?.Report(LastAnalysis);
                return ValueTask.FromResult(bookMove);
            }
        }

        ConfigureTimeLimits(limits);
        Table.NewSearch();

        Point bestMovePoint;
        int bestScore;
        int finalDepth;
        int maxTargetDepth = limits.Mode == TimeControlMode.FixedDepth ? limits.Depth : 50;

        if (ThreadCount <= 1)
        {
            (bestMovePoint, bestScore, finalDepth) = SearchSingleThreaded(
                _pb, maxTargetDepth, limits, legalMoves, cancellationToken, moveNowToken, progress, sw, state);
        }
        else
        {
            (bestMovePoint, bestScore, finalDepth) = SearchLazySmp(
                _pb, maxTargetDepth, limits, legalMoves, cancellationToken, moveNowToken, progress, sw, state);
        }

        cancellationToken.ThrowIfCancellationRequested();
        sw.Stop();

        Move chosenMove = bestMovePoint.IsValid(state.Size)
            ? new Move(bestMovePoint.X, bestMovePoint.Y, state.SideToMove)
            : legalMoves[0];

        LastAnalysis = new SearchAnalysis(
            state.MoveCount + 1,
            finalDepth,
            bestScore,
            chosenMove,
            _totalNodes,
            sw.ElapsedMilliseconds > 0 ? _totalNodes * 1000 / sw.ElapsedMilliseconds : 0,
            sw.Elapsed,
            StatusMessage: null,
            IsSearching: false);
        progress?.Report(LastAnalysis);

        return ValueTask.FromResult(chosenMove);
    }

    private (Point BestPoint, int BestScore, int FinalDepth) SearchSingleThreaded(
        BitboardPatternBoard pb,
        int maxTargetDepth,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken,
        IProgress<SearchAnalysis>? progress,
        Stopwatch sw,
        BoardState state)
    {
        Point bestMovePoint = Point.Empty;
        int bestScore = 0;
        int prevSearched = 0;
        int finalDepth = 1;

        _activeProgress = progress;
        _activeSw = sw;
        _lastReportMs = 0;
        _lastYieldMs = 0;
        _currentMoveNumber = state.MoveCount + 1;
        _currentSideToMove = state.SideToMove;
        _activeBestMovePoint = Point.Empty;
        _activeBestScore = 0;

        Span<Cand> rootCnd = stackalloc Cand[MaxCand];

        try
        {
            for (int depth = (limits.Mode == TimeControlMode.FixedDepth && limits.Depth == 1 ? 1 : 2); depth <= maxTargetDepth; depth++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _currentDepth = depth;
                long t0 = Environment.TickCount64;
                long iterNodes = 0;

                Cand best = MinimaxRoot(0, pb, depth, rootCnd, ref iterNodes, cancellationToken, moveNowToken);
                _totalNodes += iterNodes;

                cancellationToken.ThrowIfCancellationRequested();

                if (best.X >= BitboardPatternBoard.Margin && best.Y >= BitboardPatternBoard.Margin)
                {
                    bestMovePoint = new Point(best.X - BitboardPatternBoard.Margin, best.Y - BitboardPatternBoard.Margin);
                    bestScore = best.Value;
                    finalDepth = depth;
                    _activeBestMovePoint = bestMovePoint;
                    _activeBestScore = bestScore;

                    long t1 = Environment.TickCount64;
                    long td = t1 - t0;

                    long speed = td > 0 ? iterNodes * 1000 / td : 0;
                    LastAnalysis = new SearchAnalysis(
                        state.MoveCount + 1,
                        depth,
                        bestScore,
                        new Move(bestMovePoint.X, bestMovePoint.Y, state.SideToMove),
                        _totalNodes,
                        speed,
                        sw.Elapsed,
                        StatusMessage: null,
                        IsSearching: false);
                    progress?.Report(LastAnalysis);

                    if (_terminate || moveNowToken.IsCancellationRequested) break;
                    if (Math.Abs(bestScore) >= WinMin) break;
                    if (limits.Mode != TimeControlMode.FixedDepth)
                    {
                        if (t1 + TimeoutPrevent * td >= _stopTime || iterNodes == prevSearched) break;
                    }
                }
                else if (_terminate || moveNowToken.IsCancellationRequested)
                {
                    break;
                }

                prevSearched = (int)iterNodes;
            }
        }
        finally
        {
            _activeProgress = null;
            _activeSw = null;
        }

        return (bestMovePoint, bestScore, finalDepth);
    }

    private (Point BestPoint, int BestScore, int FinalDepth) SearchLazySmp(
        BitboardPatternBoard pb,
        int maxTargetDepth,
        SearchLimits limits,
        IReadOnlyList<Move> legalMoves,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken,
        IProgress<SearchAnalysis>? progress,
        Stopwatch sw,
        BoardState state)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Point masterBestPoint = Point.Empty;
        int masterBestScore = 0;
        int masterFinalDepth = 1;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, moveNowToken);
        var token = linkedCts.Token;

        int numWorkers = ThreadCount;
        Task[] helperTasks = new Task[numWorkers - 1];
        long[] workerNodes = new long[numWorkers * WorkerCounterStride];
        _activeWorkerNodes = workerNodes;
        _activeNumWorkers = numWorkers;

        BitboardPatternBoard[] workerBoards = new BitboardPatternBoard[numWorkers];
        workerBoards[0] = pb;
        for (int i = 1; i < numWorkers; i++)
        {
            workerBoards[i] = pb.Clone();
        }

        _activeProgress = progress;
        _activeSw = sw;
        _lastReportMs = 0;
        _lastYieldMs = 0;
        _currentMoveNumber = state.MoveCount + 1;
        _currentSideToMove = state.SideToMove;
        _activeBestMovePoint = Point.Empty;
        _activeBestScore = 0;

        // Workers 1..N-1: Helper scouts
        for (int i = 1; i < numWorkers; i++)
        {
            int workerId = i;
            helperTasks[workerId - 1] = Task.Run(() =>
            {
                RunHelperWorker(workerId, workerBoards[workerId], maxTargetDepth, limits, token, moveNowToken);
            });
        }

        // Worker 0: Master search on the calling thread
        Span<Cand> rootCnd = stackalloc Cand[MaxCand];
        try
        {
            int prevSearched = 0;
            for (int depth = (limits.Mode == TimeControlMode.FixedDepth && limits.Depth == 1 ? 1 : 2); depth <= maxTargetDepth; depth++)
            {
                if (token.IsCancellationRequested) break;
                _currentDepth = depth;
                long t0 = Environment.TickCount64;
                long iterNodes = 0;

                Cand best = MinimaxRoot(0, workerBoards[0], depth, rootCnd, ref iterNodes, token, moveNowToken);
                CompleteWorkerDepthNodes(0, iterNodes);

                if (token.IsCancellationRequested) break;

                if (best.X >= BitboardPatternBoard.Margin && best.Y >= BitboardPatternBoard.Margin)
                {
                    masterBestPoint = new Point(best.X - BitboardPatternBoard.Margin, best.Y - BitboardPatternBoard.Margin);
                    masterBestScore = best.Value;
                    masterFinalDepth = depth;
                    _activeBestMovePoint = masterBestPoint;
                    _activeBestScore = masterBestScore;

                    long t1 = Environment.TickCount64;
                    long td = t1 - t0;

                    long combinedNodes = GetCombinedLiveNodes(0, 0);
                    _totalNodes = combinedNodes;
                    long elapsedMs = sw.ElapsedMilliseconds;
                    long speed = elapsedMs > 0
                        ? combinedNodes * 1000 / elapsedMs
                        : (td > 0 ? combinedNodes * 1000 / td : 0);
                    LastAnalysis = new SearchAnalysis(
                        state.MoveCount + 1,
                        depth,
                        masterBestScore,
                        new Move(masterBestPoint.X, masterBestPoint.Y, state.SideToMove),
                        combinedNodes,
                        speed,
                        sw.Elapsed,
                        StatusMessage: null,
                        IsSearching: false);
                    progress?.Report(LastAnalysis);

                    if (_terminate || moveNowToken.IsCancellationRequested) break;
                    if (Math.Abs(masterBestScore) >= WinMin) break;
                    if (limits.Mode != TimeControlMode.FixedDepth)
                    {
                        if (t1 + TimeoutPrevent * td >= _stopTime || iterNodes == prevSearched) break;
                    }
                }
                else if (_terminate || moveNowToken.IsCancellationRequested)
                {
                    break;
                }

                prevSearched = (int)iterNodes;
            }
        }
        finally
        {
            _terminate = true;
            linkedCts.Cancel();
        }

        try
        {
            if (helperTasks.Length > 0)
            {
                Task.WaitAll(helperTasks);
            }
        }
        catch (AggregateException)
        {
        }
        finally
        {
            long finalSum = 0;
            for (int w = 0; w < numWorkers; w++)
            {
                finalSum += Volatile.Read(ref workerNodes[w * WorkerCounterStride]);
            }
            _totalNodes = finalSum;
            _activeWorkerNodes = null;
            _activeNumWorkers = 0;
            _activeProgress = null;
            _activeSw = null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (masterBestPoint == Point.Empty && Table.TryProbe(pb.ZobristKey, out _, out int probeEval, out _, out Point probeMove))
        {
            if (probeMove.IsValid(pb.BoardWidth, pb.BoardHeight))
            {
                masterBestPoint = probeMove;
                masterBestScore = probeEval;
            }
        }

        return (masterBestPoint, masterBestScore, masterFinalDepth);
    }

    private void RunHelperWorker(
        int workerId,
        BitboardPatternBoard pb,
        int maxTargetDepth,
        SearchLimits limits,
        CancellationToken token,
        CancellationToken moveNowToken)
    {
        int startDepth = 2 + (workerId % 2);
        Span<Cand> rootCnd = stackalloc Cand[MaxCand];

        for (int depth = startDepth; depth <= maxTargetDepth; depth++)
        {
            if (_terminate || token.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;

            int currentMasterDepth = Volatile.Read(ref _currentDepth);
            if (depth < currentMasterDepth)
            {
                depth = currentMasterDepth;
            }

            long iterNodes = 0;
            Cand best = MinimaxRoot(workerId, pb, depth, rootCnd, ref iterNodes, token, moveNowToken);
            CompleteWorkerDepthNodes(workerId, iterNodes);

            if (_terminate || token.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;
            if (Math.Abs(best.Value) >= WinMin) break;
        }
    }

    private void UpdateWorkerLiveNodes(int workerId, long iterNodes)
    {
        long[]? wn = _activeWorkerNodes;
        if (wn is not null && (uint)workerId < (uint)_activeNumWorkers)
        {
            int idx = workerId * WorkerCounterStride;
            Volatile.Write(ref wn[idx], wn[idx + 1] + iterNodes);
        }
    }

    private void CompleteWorkerDepthNodes(int workerId, long iterNodes)
    {
        long[]? wn = _activeWorkerNodes;
        if (wn is not null && (uint)workerId < (uint)_activeNumWorkers)
        {
            int idx = workerId * WorkerCounterStride;
            long completed = wn[idx + 1] + iterNodes;
            wn[idx + 1] = completed;
            Volatile.Write(ref wn[idx], completed);
        }
    }

    private long GetCombinedLiveNodes(int callerWorkerId, long callerIterNodes)
    {
        long[]? wn = _activeWorkerNodes;
        if (wn is null)
        {
            return _totalNodes + callerIterNodes;
        }

        if ((uint)callerWorkerId < (uint)_activeNumWorkers)
        {
            int idx = callerWorkerId * WorkerCounterStride;
            Volatile.Write(ref wn[idx], wn[idx + 1] + callerIterNodes);
        }

        long sum = 0;
        int n = _activeNumWorkers;
        for (int w = 0; w < n; w++)
        {
            sum += Volatile.Read(ref wn[w * WorkerCounterStride]);
        }
        return sum;
    }

    private void ConfigureTimeLimits(SearchLimits limits)
    {
        switch (limits.Mode)
        {
            case TimeControlMode.FixedDepth:
                _timeoutTurnMs = 1_000_000;
                _stopTime = _startTime + 1_000_000_000L;
                break;
            case TimeControlMode.TimePerMove:
                _timeoutTurnMs = (int)limits.Time.TotalMilliseconds;
                _stopTime = _startTime + _timeoutTurnMs - 30;
                break;
            case TimeControlMode.TimePerGame:
                long totalGameMs = (long)limits.Time.TotalMilliseconds;
                _timeoutTurnMs = (int)Math.Max(100, totalGameMs / MatchSpare);
                _stopTime = _startTime + _timeoutTurnMs - 30;
                break;
        }
    }

    private int Evaluate(BitboardPatternBoard pb)
    {
        int pBlack = 0, pWhite = 0;
        int remCount = pb.RemCount;

        for (int i = 0; i < remCount; i++)
        {
            Point pt = pb.RemPlacedPoints[i];
            ref BitboardPatternBoard.Cell c = ref pb.GetCell(pt.X, pt.Y);
            int a = (int)c.Piece;
            if (a is not (0 or 1)) continue;

            int stoneScore = 0;
            for (int k = 0; k < 4; k++)
            {
                byte patMe = c.GetPattern(k, a);
                byte patOpp = c.GetPattern(k, 1 - a);
                stoneScore += CarbonTables.Rank[CarbonTables.GetConfig(patMe, patOpp)];
            }

            if (a == 0) pBlack += stoneScore;
            else pWhite += stoneScore;
        }

        return pb.Who == PieceColor.Black ? pBlack - pWhite : pWhite - pBlack;
    }

    private int QuickWinSearch(BitboardPatternBoard pb)
    {
        int who = (int)pb.Who;
        int opp = (int)pb.Opp;

        if (pb.GetNSt(who, CarbonTables.ThreatA) >= 1) return 1;
        if (pb.GetNSt(opp, CarbonTables.ThreatA) >= 2) return -2;

        if (pb.GetNSt(opp, CarbonTables.ThreatA) == 1)
        {
            for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y; y++)
            {
                for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X; x++)
                {
                    ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                    if (c.Piece == PieceColor.Empty && c.GetStatus4(opp) == CarbonTables.ThreatA)
                    {
                        if (pb.Variant == GomokuVariant.Renju && pb.Who == pb.FirstPlayer && c.GetStatus4(who) == CarbonTables.ThreatForbid)
                        {
                            return -2;
                        }

                        pb.MakePaddedMove(x, y);
                        int q = -QuickWinSearch(pb);
                        pb.UndoMove();
                        if (q < 0) q--; else if (q > 0) q++;
                        return q;
                    }
                }
            }
        }

        if (pb.GetNSt(who, CarbonTables.ThreatB) >= 1) return 3;

        if (pb.GetNSt(who, CarbonTables.ThreatC) >= 1)
        {
            if (pb.GetNSt(opp, CarbonTables.ThreatB) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatC) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatD) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatE) == 0)
            {
                return 5;
            }

            for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y; y++)
            {
                for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X; x++)
                {
                    ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                    if (c.Piece == PieceColor.Empty && c.GetStatus4(who) == CarbonTables.ThreatC)
                    {
                        pb.MakePaddedMove(x, y);
                        int q = -QuickWinSearch(pb);
                        pb.UndoMove();
                        if (q > 0) return q + 1;
                    }
                }
            }
        }

        if (pb.GetNSt(who, CarbonTables.ThreatF) >= 1)
        {
            if (pb.GetNSt(opp, CarbonTables.ThreatB) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatC) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatD) == 0 &&
                pb.GetNSt(opp, CarbonTables.ThreatE) == 0)
            {
                return 5;
            }
        }

        return 0;
    }

    private void GenerateCand(BitboardPatternBoard pb, Span<Cand> cnd, out int nCnd, Point ttBest)
    {
        nCnd = 0;
        int who = (int)pb.Who;
        int opp = (int)pb.Opp;

        // 1. TT best move first
        if (ttBest.IsValid(pb.BoardWidth, pb.BoardHeight))
        {
            int ttX = ttBest.X + BitboardPatternBoard.Margin;
            int ttY = ttBest.Y + BitboardPatternBoard.Margin;
            if (pb.GetCell(ttX, ttY).Piece == PieceColor.Empty)
            {
                cnd[nCnd++] = new Cand(ttX, ttY, 10000);
            }
        }

        int ttFirstX = nCnd > 0 ? cnd[0].X : -1;
        int ttFirstY = nCnd > 0 ? cnd[0].Y : -1;

        // 2. Bitboard Chebyshev dilation candidate generation
        Span<uint> dilated = stackalloc uint[pb.BoardHeight];
        uint boardMask = (1u << pb.BoardWidth) - 1;
        for (int y = 0; y < pb.BoardHeight; y++)
        {
            dilated[y] = BitboardPatternBoard.DilateRow(pb.Occupied[y], boardMask);
        }

        int minY = Math.Max(0, pb.UpperLeftCand.Y - BitboardPatternBoard.Margin);
        int maxY = Math.Min(pb.BoardHeight - 1, pb.LowerRightCand.Y - BitboardPatternBoard.Margin);

        for (int by = minY; by <= maxY; by++)
        {
            uint mask = pb.GetCandidateMask(by, dilated, boardMask);
            int yp = by + BitboardPatternBoard.Margin;

            while (mask != 0)
            {
                int bx = BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                int xp = bx + BitboardPatternBoard.Margin;

                if (xp == ttFirstX && yp == ttFirstY) continue;

                ref BitboardPatternBoard.Cell c = ref pb.GetCell(xp, yp);
                short pr = c.Prior();
                if (pr > 1)
                {
                    cnd[nCnd++] = new Cand(xp, yp, pr);
                    if (nCnd >= MaxCand - 1) goto doneScan;
                }
            }
        }

    doneScan:
        // Forced tactical overrides (ONE_CAND)
        if (pb.GetNSt(who, CarbonTables.ThreatA) > 0) { ForceOneCand(pb, cnd, ref nCnd, who, CarbonTables.ThreatA); return; }
        if (pb.GetNSt(opp, CarbonTables.ThreatA) > 0) { ForceOneCand(pb, cnd, ref nCnd, opp, CarbonTables.ThreatA); return; }
        if (pb.GetNSt(who, CarbonTables.ThreatB) > 0) { ForceOneCand(pb, cnd, ref nCnd, who, CarbonTables.ThreatB); return; }

        // Block opponent open three: restrict candidates
        if (pb.GetNSt(opp, CarbonTables.ThreatB) > 0)
        {
            int oldCandCount = nCnd;
            nCnd = 0;
            for (int i = 0; i < oldCandCount; i++)
            {
                ref BitboardPatternBoard.Cell c = ref pb.GetCell(cnd[i].X, cnd[i].Y);
                byte stWho = c.GetStatus4(who);
                byte stOpp = c.GetStatus4(opp);
                if ((stWho >= CarbonTables.ThreatE && stWho != CarbonTables.ThreatForbid) ||
                    (stOpp >= CarbonTables.ThreatE && stOpp != CarbonTables.ThreatForbid))
                {
                    cnd[nCnd++] = cnd[i];
                    if (nCnd >= MaxCand - 1) break;
                }
            }
        }
    }

    private void ForceOneCand(BitboardPatternBoard pb, Span<Cand> cnd, ref int nCnd, int player, byte targetStatus)
    {
        for (int i = 0; i < nCnd; i++)
        {
            if (pb.GetCell(cnd[i].X, cnd[i].Y).GetStatus4(player) == targetStatus)
            {
                cnd[0] = cnd[i];
                nCnd = 1;
                return;
            }
        }

        for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y; y++)
        {
            for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X; x++)
            {
                ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                if (c.Piece == PieceColor.Empty && c.GetStatus4(player) == targetStatus)
                {
                    cnd[0] = new Cand(x, y, 10000);
                    nCnd = 1;
                    return;
                }
            }
        }
    }

    private Cand MinimaxRoot(
        int workerId,
        BitboardPatternBoard pb,
        int h,
        Span<Cand> rootCnd,
        ref long iterNodes,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        Cand best = new(0, 0, -Inf - 1);
        int beta = Inf;

        int q = QuickWinSearch(pb);
        if (q == 1)
        {
            int who = (int)pb.Who;
            for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y; y++)
            {
                for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X; x++)
                {
                    if (pb.GetCell(x, y).Piece == PieceColor.Empty && pb.GetCell(x, y).GetStatus4(who) == CarbonTables.ThreatA)
                    {
                        iterNodes = Math.Max(1, iterNodes);
                        return new Cand(x, y, WinMax - 1);
                    }
                }
            }
        }

        if (h == 0)
        {
            return new Cand(0, 0, Evaluate(pb));
        }

        h--;

        Point ttBest = Point.Empty;
        ulong zobristKey = pb.ZobristKey;

        if (Table.TryProbe(zobristKey, out _, out _, out _, out Point probedMove))
        {
            ttBest = probedMove;
        }

        GenerateCand(pb, rootCnd, out int nCnd, ttBest);

        bool checkRenjuForbid = pb.Variant == GomokuVariant.Renju && pb.Who == pb.FirstPlayer;

        if (nCnd > 1)
        {
            rootCnd[..nCnd].Sort((a, b) => b.Value.CompareTo(a.Value));

            if (workerId == 1 && nCnd > 2)
            {
                rootCnd.Slice(1, nCnd - 1).Reverse();
            }
            else if (workerId >= 2 && nCnd > 3)
            {
                int offset = workerId % (nCnd - 1);
                if (offset > 0)
                {
                    Cand temp = rootCnd[1];
                    rootCnd[1] = rootCnd[1 + offset];
                    rootCnd[1 + offset] = temp;
                }
            }
        }
        else if (nCnd == 1)
        {
            if (!checkRenjuForbid || pb.GetCell(rootCnd[0].X, rootCnd[0].Y).GetStatus4((int)pb.Who) != CarbonTables.ThreatForbid)
            {
                int who = (int)pb.Who;
                int opp = (int)pb.Opp;
                int score;

                if (pb.GetNSt(who, CarbonTables.ThreatA) > 0)
                {
                    score = WinMax - 1;
                }
                else if (pb.GetNSt(who, CarbonTables.ThreatB) > 0)
                {
                    score = WinMax - 3;
                }
                else if (q > 0)
                {
                    score = WinMax - q;
                }
                else if (pb.GetNSt(opp, CarbonTables.ThreatA) >= 2)
                {
                    score = -WinMax + 2;
                }
                else
                {
                    pb.MakePaddedMove(rootCnd[0].X, rootCnd[0].Y);
                    score = -Evaluate(pb);
                    pb.UndoMove();
                }

                iterNodes = Math.Max(1, iterNodes);
                return new Cand(rootCnd[0].X, rootCnd[0].Y, score);
            }
            nCnd = 0;
        }

        bool usedFallback = false;

    noCand:
        if (nCnd == 0)
        {
            usedFallback = true;
            for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y && nCnd < MaxCand; y++)
            {
                for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X && nCnd < MaxCand; x++)
                {
                    ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                    if (c.Piece == PieceColor.Empty)
                    {
                        if (checkRenjuForbid && c.GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
                            continue;
                        rootCnd[nCnd++] = new Cand(x, y, 0);
                    }
                }
            }

            if (nCnd == 0)
            {
                for (int y = BitboardPatternBoard.Margin; y < pb.BoardHeight + BitboardPatternBoard.Margin && nCnd < MaxCand; y++)
                {
                    for (int x = BitboardPatternBoard.Margin; x < pb.BoardWidth + BitboardPatternBoard.Margin && nCnd < MaxCand; x++)
                    {
                        ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                        if (c.Piece == PieceColor.Empty)
                        {
                            if (checkRenjuForbid && c.GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
                                continue;
                            rootCnd[nCnd++] = new Cand(x, y, 0);
                        }
                    }
                }
            }

            if (nCnd == 0) best.Value = 0;
        }

        BoundType bound = BoundType.UpperBound;
        Point currentBestMove = Point.Empty;
        int nForbid = 0;
        int nodeCheckCounter = 1000;

        for (int i = 0; i < nCnd; i++)
        {
            int cx = rootCnd[i].X;
            int cy = rootCnd[i].Y;

            if (checkRenjuForbid && pb.GetCell(cx, cy).GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
            {
                nForbid++;
                continue;
            }

            iterNodes++;
            pb.MakePaddedMove(cx, cy);

            int vA = -beta;
            int vB = -(best.Value + 1);
            if (vB >= WinMin) vB++;
            if (vA <= -WinMin) vA--;

            Cand m = Minimax(workerId, pb, h, vA, vB, ref nodeCheckCounter, ref iterNodes, cancellationToken, moveNowToken);
            int value = -m.Value;

            if (value >= WinMin) value--;
            if (value <= -WinMin) value++;

            pb.UndoMove();

            if (cancellationToken.IsCancellationRequested || _terminate || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
                break;
            }

            if (value > best.Value)
            {
                best = new Cand(cx, cy, value);
                currentBestMove = new Point(cx - BitboardPatternBoard.Margin, cy - BitboardPatternBoard.Margin);

                if (workerId == 0)
                {
                    _activeBestMovePoint = currentBestMove;
                    _activeBestScore = value;
                }

                if (value > beta)
                {
                    Table.Store(zobristKey, h, value, BoundType.LowerBound, currentBestMove);
                    return new Cand(cx, cy, beta + 1);
                }

                bound = BoundType.Exact;
            }

            UpdateWorkerLiveNodes(workerId, iterNodes);

            if (workerId == 0 && _activeProgress != null && _activeSw != null && _activeSw.ElapsedMilliseconds - _lastYieldMs >= 150)
            {
                _lastYieldMs = _activeSw.ElapsedMilliseconds;
                _lastReportMs = _lastYieldMs;
                long totalCurrentNodes = GetCombinedLiveNodes(0, iterNodes);
                long speed = _lastYieldMs > 0 ? totalCurrentNodes * 1000 / _lastYieldMs : 0;
                Move? currentBest = _activeBestMovePoint.IsValid(pb.BoardWidth, pb.BoardHeight)
                    ? new Move(_activeBestMovePoint.X, _activeBestMovePoint.Y, _currentSideToMove)
                    : null;

                _activeProgress.Report(new SearchAnalysis(
                    _currentMoveNumber,
                    _currentDepth,
                    _activeBestScore,
                    currentBest,
                    totalCurrentNodes,
                    speed,
                    _activeSw.Elapsed,
                    StatusMessage: "Searching...",
                    IsSearching: true));
            }

            if (_terminate || cancellationToken.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;
        }

        if (checkRenjuForbid && nForbid == nCnd && !usedFallback)
        {
            nCnd = 0;
            goto noCand;
        }

        if (!_terminate && !cancellationToken.IsCancellationRequested && !moveNowToken.IsCancellationRequested)
        {
            Table.Store(zobristKey, h, best.Value, bound, currentBestMove);
        }

        return best;
    }

    private Cand Minimax(
        int workerId,
        BitboardPatternBoard pb,
        int h,
        int alpha,
        int beta,
        ref int nodeCheckCounter,
        ref long iterNodes,
        CancellationToken cancellationToken,
        CancellationToken moveNowToken)
    {
        if (alpha > beta + 1) return new Cand(0, 0, beta + 1);
        Cand best = new(0, 0, alpha - 1);

        if (--nodeCheckCounter <= 0)
        {
            nodeCheckCounter = 1000;
            long now = Environment.TickCount64;
            UpdateWorkerLiveNodes(workerId, iterNodes);

            if (workerId == 0 && _activeProgress != null && _activeSw != null && _activeSw.ElapsedMilliseconds - _lastReportMs >= 100)
            {
                _lastReportMs = _activeSw.ElapsedMilliseconds;
                long totalCurrentNodes = GetCombinedLiveNodes(0, iterNodes);
                long speed = _lastReportMs > 0 ? totalCurrentNodes * 1000 / _lastReportMs : 0;
                Move? currentBest = _activeBestMovePoint.IsValid(pb.BoardWidth, pb.BoardHeight)
                    ? new Move(_activeBestMovePoint.X, _activeBestMovePoint.Y, _currentSideToMove)
                    : null;

                _activeProgress.Report(new SearchAnalysis(
                    _currentMoveNumber,
                    _currentDepth,
                    _activeBestScore,
                    currentBest,
                    totalCurrentNodes,
                    speed,
                    _activeSw.Elapsed,
                    StatusMessage: "Searching...",
                    IsSearching: true));
            }

            if (cancellationToken.IsCancellationRequested || now >= _stopTime || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
            }
        }

        if (_terminate || cancellationToken.IsCancellationRequested || moveNowToken.IsCancellationRequested)
        {
            return new Cand(0, 0, alpha - 1);
        }

        int q = QuickWinSearch(pb);
        if (q != 0)
        {
            return new Cand(0, 0, (q > 0 ? WinMax : -WinMax) - q);
        }

        if (h == 0)
        {
            return new Cand(0, 0, Evaluate(pb));
        }

        h--;

        Span<Cand> cnd = stackalloc Cand[MaxCand];
        Point ttBest = Point.Empty;
        ulong zobristKey = pb.ZobristKey;

        if (Table.TryProbe(zobristKey, out int ttDepth, out int ttScore, out BoundType ttBound, out Point probedMove))
        {
            ttBest = probedMove;
            if ((ttDepth >= h && ((ttDepth ^ h) & 1) == 0) || Math.Abs(ttScore) >= WinMin)
            {
                int bx = probedMove.IsValid(pb.BoardWidth, pb.BoardHeight) ? probedMove.X + BitboardPatternBoard.Margin : 0;
                int by = probedMove.IsValid(pb.BoardWidth, pb.BoardHeight) ? probedMove.Y + BitboardPatternBoard.Margin : 0;

                if (ttBound == BoundType.Exact)
                {
                    if (ttScore > beta) return new Cand(bx, by, beta + 1);
                    if (ttScore < alpha) return new Cand(0, 0, alpha - 1);
                    return new Cand(bx, by, ttScore);
                }
                if (ttBound == BoundType.LowerBound && ttScore > beta)
                {
                    return new Cand(bx, by, beta + 1);
                }
                if (ttBound == BoundType.UpperBound && ttScore < alpha)
                {
                    return new Cand(0, 0, alpha - 1);
                }
            }
        }

        GenerateCand(pb, cnd, out int nCnd, ttBest);

        bool checkRenjuForbid = pb.Variant == GomokuVariant.Renju && pb.Who == pb.FirstPlayer;

        if (nCnd > 1)
        {
            cnd[..nCnd].Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        bool usedFallback = false;

    noCand:
        if (nCnd == 0)
        {
            usedFallback = true;
            for (int y = pb.UpperLeftCand.Y; y <= pb.LowerRightCand.Y && nCnd < MaxCand; y++)
            {
                for (int x = pb.UpperLeftCand.X; x <= pb.LowerRightCand.X && nCnd < MaxCand; x++)
                {
                    ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                    if (c.Piece == PieceColor.Empty)
                    {
                        if (checkRenjuForbid && c.GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
                            continue;
                        cnd[nCnd++] = new Cand(x, y, 0);
                    }
                }
            }

            if (nCnd == 0)
            {
                for (int y = BitboardPatternBoard.Margin; y < pb.BoardHeight + BitboardPatternBoard.Margin && nCnd < MaxCand; y++)
                {
                    for (int x = BitboardPatternBoard.Margin; x < pb.BoardWidth + BitboardPatternBoard.Margin && nCnd < MaxCand; x++)
                    {
                        ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y);
                        if (c.Piece == PieceColor.Empty)
                        {
                            if (checkRenjuForbid && c.GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
                                continue;
                            cnd[nCnd++] = new Cand(x, y, 0);
                        }
                    }
                }
            }

            if (nCnd == 0) best.Value = 0;
        }

        BoundType bound = BoundType.UpperBound;
        Point currentBestMove = Point.Empty;
        int nForbid = 0;

        for (int i = 0; i < nCnd; i++)
        {
            int cx = cnd[i].X;
            int cy = cnd[i].Y;

            if (checkRenjuForbid && pb.GetCell(cx, cy).GetStatus4((int)pb.Who) == CarbonTables.ThreatForbid)
            {
                nForbid++;
                continue;
            }

            iterNodes++;
            pb.MakePaddedMove(cx, cy);

            int vA = -beta;
            int vB = -(best.Value + 1);
            if (vB >= WinMin) vB++;
            if (vA <= -WinMin) vA--;

            Cand m = Minimax(workerId, pb, h, vA, vB, ref nodeCheckCounter, ref iterNodes, cancellationToken, moveNowToken);
            int value = -m.Value;

            if (value >= WinMin) value--;
            if (value <= -WinMin) value++;

            pb.UndoMove();

            if (_terminate || cancellationToken.IsCancellationRequested || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
                break;
            }

            if (value > best.Value)
            {
                best = new Cand(cx, cy, value);
                currentBestMove = new Point(cx - BitboardPatternBoard.Margin, cy - BitboardPatternBoard.Margin);

                if (value > beta)
                {
                    Table.Store(zobristKey, h, value, BoundType.LowerBound, currentBestMove);
                    return new Cand(cx, cy, beta + 1);
                }

                bound = BoundType.Exact;
            }

            if (_terminate || cancellationToken.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;
        }

        if (checkRenjuForbid && nForbid == nCnd && !usedFallback)
        {
            nCnd = 0;
            goto noCand;
        }

        if (!_terminate && !cancellationToken.IsCancellationRequested && !moveNowToken.IsCancellationRequested)
        {
            Table.Store(zobristKey, h, best.Value, bound, currentBestMove);
        }

        return best;
    }
}

