using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// Historical experimental Gomoku engine prototyping concepts from the modern HUST Gomoku C++ engine (Gomoku-main):
/// 16-bit SIMD bitboard representation (<see cref="Bitboard15"/>), Principal Variation Search (PVS), VCF, and lock-free TT.
/// Preserved as an intermediate regression baseline. Production clients exclusively use <see cref="CarbonBitboardAiPlayer"/>.
/// </summary>
public sealed class CarbonXAiPlayer : IPlayer
{
    public const int WinBase = 1_000_000;
    public const int EvalMin = -WinBase - 1000;
    public const int EvalMax = WinBase + 1000;

    public string Name => ThreadCount > 1 ? $"Carbon-X ({ThreadCount}T)" : "Carbon-X";
    public SearchAnalysis? LastAnalysis { get; private set; }

    public LockFreeTranspositionTable Table { get; }
    public int ThreadCount { get; set; }

    // Reusable per-thread bitboard instances
    private readonly Bitboard15 _board = new();
    private long _totalNodes;
    private long _stopTime;
    private volatile bool _terminate;

    public struct ScoredMove : IComparable<ScoredMove>
    {
        public int Row;
        public int Col;
        public int Score;

        public ScoredMove(int r, int c, int s)
        {
            Row = r;
            Col = c;
            Score = s;
        }

        public int CompareTo(ScoredMove other) => other.Score.CompareTo(Score); // Descending
    }

    public CarbonXAiPlayer(int threadCount = 1, LockFreeTranspositionTable? table = null)
    {
        ThreadCount = Math.Max(1, threadCount);
        Table = table ?? new LockFreeTranspositionTable();
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
        if (legalMoves.Count == 0)
        {
            throw new InvalidOperationException("No legal moves available.");
        }

        Stopwatch sw = Stopwatch.StartNew();
        _terminate = false;
        _totalNodes = 0;

        // 1. Move 0: Play Center Stone
        if (state.MoveCount == 0)
        {
            Point center = state.Size.Center;
            Move centerMove = new(center.X, center.Y, state.SideToMove);
            LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, 0, centerMove, 1, 0, sw.Elapsed, "Center opening", IsSearching: false);
            progress?.Report(LastAnalysis);
            return ValueTask.FromResult(centerMove);
        }

        // 2. Synchronize board state into Bitboard15
        SynchronizeBoard(state);

        // 3. Opening book check
        if (limits.UseOpeningBook)
        {
            PatternBoard pb = new();
            pb.Start(state.Size.Width, state.Size.Height, state.Variant, state.FirstPlayer);
            for (int y = 0; y < state.Size.Height; y++)
            {
                for (int x = 0; x < state.Size.Width; x++)
                {
                    PieceColor p = state.GetCell(x, y);
                    if (p is PieceColor.Black or PieceColor.White)
                    {
                        pb.Who = p;
                        pb.MakeMove(x, y);
                    }
                }
            }
            pb.Who = state.SideToMove;

            if (OpeningBook.TryGetMove(pb, out Point bookPoint))
            {
                Move bookMove = new(bookPoint.X, bookPoint.Y, state.SideToMove);
                if (legalMoves.Contains(bookMove))
                {
                    LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, 0, bookMove, 1, 0, sw.Elapsed, "Opening book", IsSearching: false);
                    progress?.Report(LastAnalysis);
                    return ValueTask.FromResult(bookMove);
                }
            }
        }

        // 4. Configure time control
        ConfigureTimeLimits(limits);
        Table.NewSearch();

        // 5. Check immediate winning moves or single-reply blocks
        Point immediateMove = FindImmediateThreatResponse();
        if (immediateMove != Point.Empty && legalMoves.Any(m => m.Position == immediateMove))
        {
            Move chosen = new(immediateMove.X, immediateMove.Y, state.SideToMove);
            LastAnalysis = new SearchAnalysis(state.MoveCount + 1, 1, WinBase, chosen, 1, 0, sw.Elapsed, "Forced reply", IsSearching: false);
            progress?.Report(LastAnalysis);
            return ValueTask.FromResult(chosen);
        }

        // 6. Search Orchestrator (Single-Threaded vs Lazy SMP)
        Point bestMove = Point.Empty;
        int bestEval = 0;
        int finalDepth = 1;
        int maxDepth = limits.Mode == TimeControlMode.FixedDepth ? limits.Depth : 50;

        if (ThreadCount <= 1)
        {
            // Pure single-threaded PVS search
            (bestMove, bestEval, finalDepth) = SearchSingleThreaded(maxDepth, limits, cancellationToken, moveNowToken, progress, sw, state);
        }
        else
        {
            // Multithreaded Lazy SMP PVS search
            (bestMove, bestEval, finalDepth) = SearchLazySmp(maxDepth, limits, cancellationToken, moveNowToken, progress, sw, state);
        }

        sw.Stop();

        Move resultMove = bestMove.IsValid(state.Size)
            ? new Move(bestMove.X, bestMove.Y, state.SideToMove)
            : legalMoves[0];

        long nps = sw.ElapsedMilliseconds > 0 ? _totalNodes * 1000 / sw.ElapsedMilliseconds : 0;
        LastAnalysis = new SearchAnalysis(
            state.MoveCount + 1,
            finalDepth,
            bestEval,
            resultMove,
            _totalNodes,
            nps,
            sw.Elapsed,
            StatusMessage: null,
            IsSearching: false);
        progress?.Report(LastAnalysis);

        return ValueTask.FromResult(resultMove);
    }

    private void SynchronizeBoard(BoardState state)
    {
        _board.Clear();
        _board.SetSideToMove(state.SideToMove);
        _board.SetMoveCount(state.MoveCount);

        ulong hash = 0;
        if (state.SideToMove == PieceColor.White)
        {
            hash ^= Bitboard15.WhiteToMoveKey;
        }

        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                PieceColor p = state.GetCell(x, y);
                if (p is PieceColor.Black or PieceColor.White)
                {
                    int player = p == PieceColor.Black ? 0 : 1;
                    int posIdx = (y * Bitboard15.Size) + x;
                    var (d, dCol, _) = Bitboard15.GetDiagCoord(y, x);
                    var (ad, adCol, _) = Bitboard15.GetAdiagCoord(y, x);

                    _board.Rows[(player * 15) + y] |= (ushort)(1 << x);
                    _board.Cols[(player * 15) + x] |= (ushort)(1 << y);
                    _board.Diag[(player * 29) + d] |= (ushort)(1 << dCol);
                    _board.Adiag[(player * 29) + ad] |= (ushort)(1 << adCol);

                    hash ^= Bitboard15.GetZobristKey(player, posIdx);
                }
            }
        }

        _board.SetHash(hash);
    }

    private void ConfigureTimeLimits(SearchLimits limits)
    {
        long now = Environment.TickCount64;
        switch (limits.Mode)
        {
            case TimeControlMode.FixedDepth:
                _stopTime = now + 1_000_000_000L;
                break;
            case TimeControlMode.TimePerMove:
                _stopTime = now + (long)limits.Time.TotalMilliseconds - 20;
                break;
            case TimeControlMode.TimePerGame:
                long totalGameMs = (long)limits.Time.TotalMilliseconds;
                long turnMs = Math.Max(100, totalGameMs / 10);
                _stopTime = now + turnMs - 20;
                break;
        }
    }

    private (Point BestMove, int BestEval, int FinalDepth) SearchSingleThreaded(
        int maxDepth,
        SearchLimits limits,
        CancellationToken ct,
        CancellationToken moveNowToken,
        IProgress<SearchAnalysis>? progress,
        Stopwatch sw,
        BoardState state)
    {
        return SearchWorker(0, _board, maxDepth, limits, ct, moveNowToken, progress, sw, state);
    }

    private (Point BestMove, int BestEval, int FinalDepth) SearchLazySmp(
        int maxDepth,
        SearchLimits limits,
        CancellationToken ct,
        CancellationToken moveNowToken,
        IProgress<SearchAnalysis>? progress,
        Stopwatch sw,
        BoardState state)
    {
        Point masterBestMove = Point.Empty;
        int masterBestEval = EvalMin;
        int masterFinalDepth = 1;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, moveNowToken);
        var token = cts.Token;

        int numWorkers = ThreadCount;
        Task[] tasks = new Task[numWorkers];

        // Pre-clone all boards BEFORE launching any tasks to prevent race conditions!
        Bitboard15[] workerBoards = new Bitboard15[numWorkers];
        for (int i = 0; i < numWorkers; i++)
        {
            workerBoards[i] = _board.Clone();
        }

        // Worker 0: Master PVS search
        tasks[0] = Task.Run(() =>
        {
            (masterBestMove, masterBestEval, masterFinalDepth) = SearchWorker(
                0, workerBoards[0], maxDepth, limits, token, moveNowToken, progress, sw, state);
            cts.Cancel(); // Signal helper threads when master finishes
        });

        // Workers 1..N-1: Helper scouts
        for (int workerId = 1; workerId < numWorkers; workerId++)
        {
            int wid = workerId;
            tasks[wid] = Task.Run(() =>
            {
                SearchWorker(wid, workerBoards[wid], maxDepth, limits, token, moveNowToken, null, sw, state);
            });
        }

        Task.WaitAll(tasks);
        return (masterBestMove, masterBestEval, masterFinalDepth);
    }

    private (Point BestMove, int BestEval, int FinalDepth) SearchWorker(
        int workerId,
        Bitboard15 board,
        int maxDepth,
        SearchLimits limits,
        CancellationToken token,
        CancellationToken moveNowToken,
        IProgress<SearchAnalysis>? progress,
        Stopwatch sw,
        BoardState state)
    {
        Point currentBestMove = Point.Empty;
        int currentBestEval = EvalMin;
        int finalDepth = 1;
        Span<Point> oppFives = stackalloc Point[16];

        bool isMaster = workerId == 0;
        int startDepth = isMaster ? 1 : 1 + (workerId % 2);

        for (int depth = startDepth; depth <= maxDepth; depth++)
        {
            if (_terminate || token.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;
            if (limits.Mode != TimeControlMode.FixedDepth && Environment.TickCount64 >= _stopTime)
            {
                _terminate = true;
                break;
            }

            finalDepth = depth;
            int alpha = EvalMin;
            int beta = EvalMax;

            Point depthBestMove = Point.Empty;
            int depthBestEval = EvalMin;

            int player = board.SideToMove == PieceColor.Black ? 0 : 1;
            int opp = 1 - player;
            int oppFiveCount = board.GetAllFiveMoves(opp, oppFives);

            List<ScoredMove> candidates;
            if (oppFiveCount > 0)
            {
                candidates = new List<ScoredMove>(oppFiveCount);
                for (int d = 0; d < oppFiveCount; d++)
                {
                    candidates.Add(new ScoredMove(oppFives[d].Y, oppFives[d].X, 200_000));
                }
            }
            else
            {
                candidates = GenerateOrderedCandidates(board, currentBestMove);
            }
            if (candidates.Count == 0) break;

            // Perturb candidates for helper workers to explore diverse branches
            if (!isMaster && candidates.Count > 2 && workerId % 2 == 1)
            {
                (candidates[0], candidates[1]) = (candidates[1], candidates[0]);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (_terminate || token.IsCancellationRequested || moveNowToken.IsCancellationRequested) break;
                if (limits.Mode != TimeControlMode.FixedDepth && Environment.TickCount64 >= _stopTime)
                {
                    _terminate = true;
                    break;
                }

                var cand = candidates[i];
                board.MakeMove(cand.Row, cand.Col);
                Interlocked.Increment(ref _totalNodes);

                int score;
                if (i == 0)
                {
                    score = -PvsSearch(board, depth - 1, -beta, -alpha, token);
                }
                else
                {
                    score = -PvsSearch(board, depth - 1, -alpha - 1, -alpha, token);
                    if (score > alpha && score < beta && !_terminate)
                    {
                        score = -PvsSearch(board, depth - 1, -beta, -alpha, token);
                    }
                }

                board.UndoMove();

                if (_terminate || token.IsCancellationRequested) break;

                if (score > depthBestEval)
                {
                    depthBestEval = score;
                    depthBestMove = new Point(cand.Col, cand.Row);
                }

                if (score > alpha)
                {
                    alpha = score;
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            if (!_terminate && !token.IsCancellationRequested && depthBestMove != Point.Empty)
            {
                currentBestMove = depthBestMove;
                currentBestEval = depthBestEval;

                Table.Store(board.Hash, depth, currentBestEval, BoundType.Exact, currentBestMove);

                if (isMaster)
                {
                    long nps = sw.ElapsedMilliseconds > 0 ? _totalNodes * 1000 / sw.ElapsedMilliseconds : 0;
                    LastAnalysis = new SearchAnalysis(
                        state.MoveCount + 1,
                        depth,
                        currentBestEval,
                        new Move(currentBestMove.X, currentBestMove.Y, state.SideToMove),
                        _totalNodes,
                        nps,
                        sw.Elapsed,
                        StatusMessage: null,
                        IsSearching: true);
                    progress?.Report(LastAnalysis);
                }

                if (Math.Abs(currentBestEval) >= WinBase - 100) break;
            }
            else
            {
                break;
            }
        }

        return (currentBestMove, currentBestEval, finalDepth);
    }

    private int PvsSearch(Bitboard15 board, int depth, int alpha, int beta, CancellationToken ct)
    {
        if ((_totalNodes & 1023) == 0)
        {
            if (Environment.TickCount64 >= _stopTime || ct.IsCancellationRequested)
            {
                _terminate = true;
                return 0;
            }
        }

        if (board.HasWinner)
        {
            return -WinBase - depth;
        }

        int player = board.SideToMove == PieceColor.Black ? 0 : 1;
        int opp = 1 - player;

        // 1. Can CURRENT PLAYER win this turn with a Five?
        Span<Point> playerFives = stackalloc Point[16];
        int myFiveCount = board.GetAllFiveMoves(player, playerFives);
        if (myFiveCount > 0)
        {
            return WinBase + depth;
        }

        // 2. Does OPPONENT have immediate Five threats that we MUST block?
        Span<Point> oppFives = stackalloc Point[16];
        int oppFiveCount = board.GetAllFiveMoves(opp, oppFives);
        if (oppFiveCount >= 2)
        {
            return -WinBase - depth;
        }
        if (oppFiveCount == 1)
        {
            Point blockMove = oppFives[0];
            board.MakeMove(blockMove.Y, blockMove.X);
            Interlocked.Increment(ref _totalNodes);
            int score = -PvsSearch(board, depth - 1, -beta, -alpha, ct);
            board.UndoMove();
            return score;
        }

        // 3. Leaf evaluation
        if (depth <= 0)
        {
            return board.Evaluate();
        }

        // 4. Transposition Table Probe
        Point ttMove = Point.Empty;
        if (Table.TryProbe(board.Hash, out int ttDepth, out int ttEval, out BoundType ttBound, out Point probedMove))
        {
            ttMove = probedMove;
            if (ttDepth >= depth)
            {
                if (ttBound == BoundType.Exact) return ttEval;
                if (ttBound == BoundType.LowerBound && ttEval >= beta) return ttEval;
                if (ttBound == BoundType.UpperBound && ttEval <= alpha) return ttEval;
            }
        }

        BoundType bound = BoundType.UpperBound;
        Point bestDepthMove = Point.Empty;
        int maxEval = EvalMin;

        var candidates = GenerateOrderedCandidates(board, ttMove);
        if (candidates.Count == 0) return 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            var cand = candidates[i];
            board.MakeMove(cand.Row, cand.Col);
            Interlocked.Increment(ref _totalNodes);

            int score;
            if (i == 0)
            {
                score = -PvsSearch(board, depth - 1, -beta, -alpha, ct);
            }
            else
            {
                // Null-window scout
                score = -PvsSearch(board, depth - 1, -alpha - 1, -alpha, ct);
                if (score > alpha && score < beta && !_terminate)
                {
                    score = -PvsSearch(board, depth - 1, -beta, -alpha, ct);
                }
            }

            board.UndoMove();

            if (_terminate) return 0;

            if (score > maxEval)
            {
                maxEval = score;
                bestDepthMove = new Point(cand.Col, cand.Row);
            }

            if (maxEval > alpha)
            {
                alpha = maxEval;
                bound = BoundType.Exact;
            }

            if (alpha >= beta)
            {
                bound = BoundType.LowerBound;
                break;
            }
        }

        if (!_terminate && bestDepthMove != Point.Empty)
        {
            Table.Store(board.Hash, depth, maxEval, bound, bestDepthMove);
        }

        return maxEval;
    }

    private List<ScoredMove> GenerateOrderedCandidates(Bitboard15 board, Point ttBest)
    {
        List<ScoredMove> list = new(32);
        int player = board.SideToMove == PieceColor.Black ? 0 : 1;
        int opp = 1 - player;

        // Fast SIMD bitwise Chebyshev distance 2 expansion
        Span<ushort> rExp = stackalloc ushort[Bitboard15.Size];
        int stonesFound = 0;

        for (int r = 0; r < Bitboard15.Size; r++)
        {
            ushort o = board.GetOccupiedRow(r);
            if (o != 0) stonesFound++;
            rExp[r] = (ushort)((o | (o << 1) | (o >> 1) | (o << 2) | (o >> 2)) & 0x7FFF);
        }

        if (stonesFound == 0)
        {
            list.Add(new ScoredMove(7, 7, 100));
            return list;
        }

        for (int r = 0; r < Bitboard15.Size; r++)
        {
            ushort rCand = rExp[r];
            if (r > 0) rCand |= rExp[r - 1];
            if (r > 1) rCand |= rExp[r - 2];
            if (r < Bitboard15.Size - 1) rCand |= rExp[r + 1];
            if (r < Bitboard15.Size - 2) rCand |= rExp[r + 2];

            // Only consider empty neighbor cells
            rCand &= (ushort)(~board.GetOccupiedRow(r) & 0x7FFF);

            while (rCand != 0)
            {
                int c = BitOperations.TrailingZeroCount(rCand);
                rCand &= (ushort)(rCand - 1);

                int score = ScoreIntersection(board, r, c, player, opp);
                if (score <= 0) continue; // Prune completely inert squares

                if (ttBest.X == c && ttBest.Y == r)
                {
                    score += 500_000;
                }

                list.Add(new ScoredMove(r, c, score));
            }
        }

        list.Sort();
        if (list.Count > 16)
        {
            list.RemoveRange(16, list.Count - 16);
        }

        if (list.Count == 0)
        {
            list.Add(new ScoredMove(7, 7, 100));
        }

        return list;
    }

    private int ScoreIntersection(Bitboard15 board, int r, int c, int player, int opp)
    {
        int myScore = EvaluatePointThreat(board, r, c, player);
        int oppScore = EvaluatePointThreat(board, r, c, opp);
        return myScore + (int)(1.2 * oppScore);
    }

    private int EvaluatePointThreat(Bitboard15 board, int r, int c, int player)
    {
        var (d, dCol, dLen) = Bitboard15.GetDiagCoord(r, c);
        var (ad, adCol, adLen) = Bitboard15.GetAdiagCoord(r, c);

        int tRow = LineThreat(board.Rows[(player * 15) + r], board.GetOccupiedRow(r), c, 15);
        int tCol = LineThreat(board.Cols[(player * 15) + c], board.GetOccupiedCol(c), r, 15);
        int tDiag = LineThreat(board.Diag[(player * 29) + d], (ushort)(board.Diag[d] | board.Diag[29 + d]), dCol, dLen);
        int tAdiag = LineThreat(board.Adiag[(player * 29) + ad], (ushort)(board.Adiag[ad] | board.Adiag[29 + ad]), adCol, adLen);

        int tMax = Math.Max(Math.Max(tRow, tCol), Math.Max(tDiag, tAdiag));
        int tSum = tRow + tCol + tDiag + tAdiag;

        return (tMax * 2) + tSum;
    }

    private int LineThreat(ushort myMask, ushort occMask, int index, int length)
    {
        if (length < 5) return 0;
        int toggled = myMask | (1 << index);
        int toggledOcc = occMask | (1 << index);

        int maxScore = 0;
        int startJ = Math.Max(index - 4, 0);
        int endJ = Math.Min(index, length - 5);

        // 5-windows for 5 and closed 4/3/2
        for (int j = startJ; j <= endJ; j++)
        {
            int wMy = (toggled >> j) & 0x1F;
            int wOcc = (toggledOcc >> j) & 0x1F;
            if (wOcc == wMy) // No opponent stone
            {
                int c = BitOperations.PopCount((uint)wMy);
                int sc = c switch
                {
                    5 => 50_000,
                    4 => 4_000,
                    3 => 400,
                    2 => 30,
                    _ => 0
                };
                if (sc > maxScore) maxScore = sc;
            }
        }

        // 6-windows for open 4 and open 3
        int start6 = Math.Max(index - 4, 0);
        int end6 = Math.Min(index - 1, length - 6);
        for (int j = start6; j <= end6; j++)
        {
            if (((toggledOcc >> j) & 1) == 0 && ((toggledOcc >> (j + 5)) & 1) == 0)
            {
                int midMy = (toggled >> (j + 1)) & 0x0F;
                int midOcc = (toggledOcc >> (j + 1)) & 0x0F;
                if (midOcc == midMy)
                {
                    int midC = BitOperations.PopCount((uint)midMy);
                    int sc = midC switch
                    {
                        4 => 25_000, // Open four!
                        3 => 2_000,  // Open three!
                        2 => 150,    // Open two!
                        _ => 0
                    };
                    if (sc > maxScore) maxScore = sc;
                }
            }
        }

        return maxScore;
    }

    private Point FindImmediateThreatResponse()
    {
        int player = _board.SideToMove == PieceColor.Black ? 0 : 1;
        int opp = 1 - player;

        // 1. Can we win this turn with a Five?
        Span<Point> fives = stackalloc Point[16];
        int myFives = _board.GetAllFiveMoves(player, fives);
        if (myFives > 0) return fives[0];

        // 2. Must we defend against opponent Five?
        int oppFives = _board.GetAllFiveMoves(opp, fives);
        if (oppFives > 0) return fives[0];

        return Point.Empty;
    }
}
