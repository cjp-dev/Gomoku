using System.Diagnostics;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// High-performance Gomoku AI ported directly from Michał Czardybon's Carbon Gomoku.
/// Features directional pattern bitboards, precomputed threat tensors, VCF quickWinSearch,
/// expert tactical move pruning, Zobrist Transposition Table, and iterative deepening.
/// </summary>
public sealed class CarbonAiPlayer : IPlayer
{
    public const int WinMin = 25000;
    public const int WinMax = 30000;
    public const int Inf = 32000;
    public const int MaxCand = 256;
    public const int MatchSpare = 7;
    public const int TimeoutPrevent = 5;

    public string Name => "Carbon AI";
    public SearchAnalysis? LastAnalysis { get; private set; }

    public TranspositionTable Table { get; }

    private readonly PatternBoard _pb = new();
    private long _totalNodes;
    private long _iterNodes;
    private long _startTime;
    private long _stopTime;
    private int _timeoutTurnMs = 1000000;
    private bool _terminate;

    private IProgress<SearchAnalysis>? _activeProgress;
    private Stopwatch? _activeSw;
    private long _lastReportMs;
    private long _lastYieldMs;
    private int _currentDepth;
    private int _currentMoveNumber;
    private PieceColor _currentSideToMove;
    private Point _activeBestMovePoint;
    private int _activeBestScore;
    private readonly Cand[] _rootCnd = new Cand[MaxCand];

    public struct Cand
    {
        public int X, Y;
        public int Value;
        public Cand(int x, int y, int v) { X = x; Y = y; Value = v; }
    }

    public CarbonAiPlayer(TranspositionTable? table = null)
    {
        Table = table ?? new TranspositionTable(TranspositionTable.DefaultEntries);
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
        _startTime = Environment.TickCount64;
        _terminate = false;
        _totalNodes = 0;

        // 1. First move center opening
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
        // Play existing stones into pattern board
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

        // Compute time limits
        ConfigureTimeLimits(limits);

        Table.NewSearch();

        Point bestMovePoint = Point.Empty;
        int bestScore = 0;
        int maxTargetDepth = limits.Mode == TimeControlMode.FixedDepth ? limits.Depth : 50;

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

        try
        {
            for (int depth = (limits.Mode == TimeControlMode.FixedDepth && limits.Depth == 1 ? 1 : 2); depth <= maxTargetDepth; depth++)
            {
                _currentDepth = depth;
                long t0 = Environment.TickCount64;
                _iterNodes = 0;

                Cand best = MinimaxRoot(depth, cancellationToken, moveNowToken);
                _totalNodes += _iterNodes;

                cancellationToken.ThrowIfCancellationRequested();

                if (best.X >= PatternBoard.Margin && best.Y >= PatternBoard.Margin &&
                    (!_terminate || !bestMovePoint.IsValid(state.Size)))
                {
                    bestMovePoint = new Point(best.X - PatternBoard.Margin, best.Y - PatternBoard.Margin);
                    bestScore = best.Value;
                    finalDepth = depth;
                    _activeBestMovePoint = bestMovePoint;
                    _activeBestScore = bestScore;
                }

                long t1 = Environment.TickCount64;
                long td = t1 - t0;

                long speed = td > 0 ? _iterNodes * 1000 / td : 0;
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
                // Win found or search timed out
                if (Math.Abs(bestScore) >= WinMin) break;
                if (limits.Mode != TimeControlMode.FixedDepth)
                {
                    if (t1 + TimeoutPrevent * td >= _stopTime || _iterNodes == prevSearched) break;
                }

                prevSearched = (int)_iterNodes;
            }
        }
        finally
        {
            _activeProgress = null;
            _activeSw = null;
        }

        sw.Stop();

        // Fallback to first legal move if no candidate found
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

    private int Evaluate()
    {
        int pBlack = 0, pWhite = 0;
        int remCount = _pb.RemCount;

        for (int i = 0; i < remCount; i++)
        {
            Point pt = _pb.RemPlacedPoints[i];
            ref PatternBoard.Cell c = ref _pb.Cells[pt.X, pt.Y];
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

        return _pb.Who == PieceColor.Black ? pBlack - pWhite : pWhite - pBlack;
    }

    private int QuickWinSearch()
    {
        int who = (int)_pb.Who;
        int opp = (int)_pb.Opp;

        if (_pb.NSt[who, CarbonTables.ThreatA] >= 1) return 1;  // Immediate win
        if (_pb.NSt[opp, CarbonTables.ThreatA] >= 2) return -2; // Opponent has double threat - cannot block both

        if (_pb.NSt[opp, CarbonTables.ThreatA] == 1) // Must block opponent's four
        {
            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
                {
                    ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                    if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0) && c.GetStatus4(opp) == CarbonTables.ThreatA)
                    {
                        if (_pb.Variant == GomokuVariant.Renju && _pb.Who == _pb.FirstPlayer && c.GetStatus4(who) == CarbonTables.ThreatForbid)
                        {
                            return -2; // Cannot defend due to forbidden square
                        }

                        _pb.MakePaddedMove(x, y);
                        int q = -QuickWinSearch();
                        _pb.UndoMove();
                        if (q < 0) q--; else if (q > 0) q++;
                        return q;
                    }
                }
            }
        }

        if (_pb.NSt[who, CarbonTables.ThreatB] >= 1) return 3; // Open four creates unstoppable win

        if (_pb.NSt[who, CarbonTables.ThreatC] >= 1)
        {
            if (_pb.NSt[opp, CarbonTables.ThreatB] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatC] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatD] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatE] == 0)
            {
                return 5;
            }

            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
                {
                    ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                    if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0) && c.GetStatus4(who) == CarbonTables.ThreatC)
                    {
                        _pb.MakePaddedMove(x, y);
                        int q = -QuickWinSearch();
                        _pb.UndoMove();
                        if (q > 0) return q + 1;
                    }
                }
            }
        }

        if (_pb.NSt[who, CarbonTables.ThreatF] >= 1)
        {
            if (_pb.NSt[opp, CarbonTables.ThreatB] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatC] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatD] == 0 &&
                _pb.NSt[opp, CarbonTables.ThreatE] == 0)
            {
                return 5;
            }
        }

        return 0;
    }

    private void GenerateCand(Span<Cand> cnd, out int nCnd, Point ttBest)
    {
        nCnd = 0;
        int who = (int)_pb.Who;
        int opp = (int)_pb.Opp;

        // 1. TT best move first
        if (ttBest.IsValid(_pb.BoardWidth, _pb.BoardHeight))
        {
            int ttX = ttBest.X + PatternBoard.Margin;
            int ttY = ttBest.Y + PatternBoard.Margin;
            if (_pb.Cells[ttX, ttY].Piece == PieceColor.Empty)
            {
                cnd[nCnd++] = new Cand(ttX, ttY, 10000);
            }
        }

        int ttFirstX = nCnd > 0 ? cnd[0].X : -1;
        int ttFirstY = nCnd > 0 ? cnd[0].Y : -1;

        // 2. Scan bounding box
        for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
        {
            for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
            {
                if (x == ttFirstX && y == ttFirstY) continue;

                ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0))
                {
                    short pr = c.Prior();
                    if (pr > 1)
                    {
                        cnd[nCnd++] = new Cand(x, y, pr);
                        if (nCnd >= MaxCand - 1) break;
                    }
                }
            }
            if (nCnd >= MaxCand - 1) break;
        }

        // Forced tactical overrides (ONE_CAND)
        if (_pb.NSt[who, CarbonTables.ThreatA] > 0) { ForceOneCand(cnd, ref nCnd, who, CarbonTables.ThreatA); return; }
        if (_pb.NSt[opp, CarbonTables.ThreatA] > 0) { ForceOneCand(cnd, ref nCnd, opp, CarbonTables.ThreatA); return; }
        if (_pb.NSt[who, CarbonTables.ThreatB] > 0) { ForceOneCand(cnd, ref nCnd, who, CarbonTables.ThreatB); return; }

        // Block opponent open three: restrict candidates
        if (_pb.NSt[opp, CarbonTables.ThreatB] > 0)
        {
            nCnd = 0;
            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
                {
                    ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                    if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0))
                    {
                        byte stWho = c.GetStatus4(who);
                        byte stOpp = c.GetStatus4(opp);
                        if ((stWho >= CarbonTables.ThreatE && stWho != CarbonTables.ThreatForbid) ||
                            (stOpp >= CarbonTables.ThreatE && stOpp != CarbonTables.ThreatForbid))
                        {
                            short pr = c.Prior();
                            if (pr > 0)
                            {
                                cnd[nCnd++] = new Cand(x, y, pr);
                                if (nCnd >= MaxCand - 1) break;
                            }
                        }
                    }
                }
                if (nCnd >= MaxCand - 1) break;
            }
        }
    }

    private void ForceOneCand(Span<Cand> cnd, ref int nCnd, int player, byte targetStatus)
    {
        for (int i = 0; i < nCnd; i++)
        {
            if (_pb.Cells[cnd[i].X, cnd[i].Y].GetStatus4(player) == targetStatus)
            {
                cnd[0] = cnd[i];
                nCnd = 1;
                return;
            }
        }

        for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
        {
            for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
            {
                ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                if (c.Piece == PieceColor.Empty && c.GetStatus4(player) == targetStatus)
                {
                    cnd[0] = new Cand(x, y, 10000);
                    nCnd = 1;
                    return;
                }
            }
        }
    }

    private int _nodeCheckCounter = 1000;

    private Cand MinimaxRoot(int h, CancellationToken cancellationToken, CancellationToken moveNowToken)
    {
        Cand best = new(0, 0, -Inf - 1);
        int beta = Inf;

        // Quick win recognition
        int q = QuickWinSearch();
        if (q == 1) // Immediate win available
        {
            int who = (int)_pb.Who;
            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X; x++)
                {
                    if (_pb.Cells[x, y].Piece == PieceColor.Empty && _pb.Cells[x, y].GetStatus4(who) == CarbonTables.ThreatA)
                    {
                        _iterNodes = Math.Max(1, _iterNodes);
                        return new Cand(x, y, WinMax - 1);
                    }
                }
            }
        }

        if (h == 0)
        {
            return new Cand(0, 0, Evaluate());
        }

        h--;

        Point ttBest = Point.Empty;
        ulong zobristKey = _pb.ZobristKey;

        if (Table.TryProbe(zobristKey, out TranspositionEntry entry))
        {
            ttBest = entry.BestMove;
        }

        GenerateCand(_rootCnd.AsSpan(), out int nCnd, ttBest);

        bool checkRenjuForbid = _pb.Variant == GomokuVariant.Renju && _pb.Who == _pb.FirstPlayer;

        if (nCnd > 1)
        {
            Array.Sort(_rootCnd, 0, nCnd, Comparer<Cand>.Create((a, b) => b.Value.CompareTo(a.Value)));
        }
        else if (nCnd == 1)
        {
            if (!checkRenjuForbid || _pb.Cells[_rootCnd[0].X, _rootCnd[0].Y].GetStatus4((int)_pb.Who) != CarbonTables.ThreatForbid)
            {
                int who = (int)_pb.Who;
                int opp = (int)_pb.Opp;
                int score;

                if (_pb.NSt[who, CarbonTables.ThreatA] > 0)
                {
                    score = WinMax - 1; // Immediate 5-in-a-row (Mate in 1)
                }
                else if (_pb.NSt[who, CarbonTables.ThreatB] > 0)
                {
                    score = WinMax - 3; // Open four creates unstoppable win in 3 plies (Mate in 3)
                }
                else if (q > 0)
                {
                    score = WinMax - q; // QuickWinSearch forced tactical mate
                }
                else if (_pb.NSt[opp, CarbonTables.ThreatA] >= 2)
                {
                    score = -WinMax + 2; // Opponent has unstoppable double-four threat
                }
                else
                {
                    // Forced defensive move (e.g. blocking opponent's single four); evaluate resulting position
                    _pb.MakePaddedMove(_rootCnd[0].X, _rootCnd[0].Y);
                    score = -Evaluate();
                    _pb.UndoMove();
                }

                _iterNodes = Math.Max(1, _iterNodes);
                return new Cand(_rootCnd[0].X, _rootCnd[0].Y, score);
            }
            nCnd = 0;
        }

        bool usedFallback = false;

    noCand:
        if (nCnd == 0)
        {
            usedFallback = true;
            // Board nearly full or all primary candidates forbidden: include all adjacent candidates
            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y && nCnd < MaxCand; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X && nCnd < MaxCand; x++)
                {
                    ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                    if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0))
                    {
                        if (checkRenjuForbid && c.GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
                            continue;
                        _rootCnd[nCnd++] = new Cand(x, y, 0);
                    }
                }
            }

            // If still empty (e.g. only non-adjacent squares remain legal), scan full board
            if (nCnd == 0)
            {
                for (int y = PatternBoard.Margin; y < _pb.BoardHeight + PatternBoard.Margin && nCnd < MaxCand; y++)
                {
                    for (int x = PatternBoard.Margin; x < _pb.BoardWidth + PatternBoard.Margin && nCnd < MaxCand; x++)
                    {
                        ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                        if (c.Piece == PieceColor.Empty)
                        {
                            if (checkRenjuForbid && c.GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
                                continue;
                            _rootCnd[nCnd++] = new Cand(x, y, 0);
                        }
                    }
                }
            }

            if (nCnd == 0) best.Value = 0; // Board is full draw
        }

        BoundType bound = BoundType.UpperBound;
        Point currentBestMove = Point.Empty;
        int nForbid = 0;

        for (int i = 0; i < nCnd; i++)
        {
            int cx = _rootCnd[i].X;
            int cy = _rootCnd[i].Y;

            // Renju check: Black cannot play on forbidden squares
            if (checkRenjuForbid && _pb.Cells[cx, cy].GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
            {
                nForbid++;
                continue;
            }

            _iterNodes++;
            _pb.MakePaddedMove(cx, cy);

            int vA = -beta;
            int vB = -(best.Value + 1);
            if (vB >= WinMin) vB++;
            if (vA <= -WinMin) vA--;

            Cand m = Minimax(h, vA, vB, cancellationToken, moveNowToken);
            int value = -m.Value;

            if (value >= WinMin) value--;
            if (value <= -WinMin) value++;

            _pb.UndoMove();

            cancellationToken.ThrowIfCancellationRequested();
            if (_terminate || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
                break;
            }

            if (value > best.Value)
            {
                best = new Cand(cx, cy, value);
                currentBestMove = new Point(cx - PatternBoard.Margin, cy - PatternBoard.Margin);
                _activeBestMovePoint = currentBestMove;
                _activeBestScore = value;

                if (value > beta)
                {
                    Table.Store(zobristKey, h, value, BoundType.LowerBound, currentBestMove);
                    return new Cand(cx, cy, beta + 1); // Closed-interval beta cutoff
                }

                bound = BoundType.Exact;
            }

            if (_activeProgress != null && _activeSw != null && _activeSw.ElapsedMilliseconds - _lastYieldMs >= 150)
            {
                _lastYieldMs = _activeSw.ElapsedMilliseconds;
                _lastReportMs = _lastYieldMs;
                long totalCurrentNodes = _totalNodes + _iterNodes;
                long speed = _lastYieldMs > 0 ? totalCurrentNodes * 1000 / _lastYieldMs : 0;
                Move? currentBest = _activeBestMovePoint.IsValid(_pb.BoardWidth, _pb.BoardHeight)
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

    private Cand Minimax(int h, int alpha, int beta, CancellationToken cancellationToken, CancellationToken moveNowToken)
    {
        // Closed interval [alpha, beta] as in AICarbon::minimax
        if (alpha > beta + 1) return new Cand(0, 0, beta + 1);
        Cand best = new(0, 0, alpha - 1);

        if (--_nodeCheckCounter <= 0)
        {
            _nodeCheckCounter = 1000;
            long now = Environment.TickCount64;

            if (_activeProgress != null && _activeSw != null && _activeSw.ElapsedMilliseconds - _lastReportMs >= 100)
            {
                _lastReportMs = _activeSw.ElapsedMilliseconds;
                long totalCurrentNodes = _totalNodes + _iterNodes;
                long speed = _lastReportMs > 0 ? totalCurrentNodes * 1000 / _lastReportMs : 0;
                Move? currentBest = _activeBestMovePoint.IsValid(_pb.BoardWidth, _pb.BoardHeight)
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

            cancellationToken.ThrowIfCancellationRequested();
            if (now >= _stopTime || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
            }
        }

        // Quick win recognition
        int q = QuickWinSearch();
        if (q != 0)
        {
            return new Cand(0, 0, (q > 0 ? WinMax : -WinMax) - q);
        }

        if (h == 0)
        {
            return new Cand(0, 0, Evaluate());
        }

        h--;

        Span<Cand> cnd = stackalloc Cand[MaxCand];
        Point ttBest = Point.Empty;
        ulong zobristKey = _pb.ZobristKey;

        if (Table.TryProbe(zobristKey, out TranspositionEntry entry))
        {
            ttBest = entry.BestMove;
            if ((entry.Depth >= h && ((entry.Depth ^ h) & 1) == 0) || Math.Abs(entry.Score) >= WinMin)
            {
                int bx = entry.BestMove.IsValid(_pb.BoardWidth, _pb.BoardHeight) ? entry.BestMove.X + PatternBoard.Margin : 0;
                int by = entry.BestMove.IsValid(_pb.BoardWidth, _pb.BoardHeight) ? entry.BestMove.Y + PatternBoard.Margin : 0;

                if (entry.Bound == BoundType.Exact)
                {
                    if (entry.Score > beta) return new Cand(bx, by, beta + 1);
                    if (entry.Score < alpha) return new Cand(0, 0, alpha - 1);
                    return new Cand(bx, by, entry.Score);
                }
                if (entry.Bound == BoundType.LowerBound && entry.Score > beta)
                {
                    return new Cand(bx, by, beta + 1);
                }
                if (entry.Bound == BoundType.UpperBound && entry.Score < alpha)
                {
                    return new Cand(0, 0, alpha - 1);
                }
            }
        }

        GenerateCand(cnd, out int nCnd, ttBest);

        bool checkRenjuForbid = _pb.Variant == GomokuVariant.Renju && _pb.Who == _pb.FirstPlayer;

        if (nCnd > 1)
        {
            // Quicksort candidates descending by value (priority)
            cnd[..nCnd].Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        bool usedFallback = false;

    noCand:
        if (nCnd == 0)
        {
            usedFallback = true;
            // Board nearly full or all primary candidates forbidden: include all adjacent candidates
            for (int y = _pb.UpperLeftCand.Y; y <= _pb.LowerRightCand.Y && nCnd < MaxCand; y++)
            {
                for (int x = _pb.UpperLeftCand.X; x <= _pb.LowerRightCand.X && nCnd < MaxCand; x++)
                {
                    ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                    if (c.Piece == PieceColor.Empty && (c.Adj1 != 0 || c.Adj2 != 0))
                    {
                        if (checkRenjuForbid && c.GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
                            continue;
                        cnd[nCnd++] = new Cand(x, y, 0);
                    }
                }
            }

            // If still empty (e.g. only non-adjacent squares remain legal), scan full board
            if (nCnd == 0)
            {
                for (int y = PatternBoard.Margin; y < _pb.BoardHeight + PatternBoard.Margin && nCnd < MaxCand; y++)
                {
                    for (int x = PatternBoard.Margin; x < _pb.BoardWidth + PatternBoard.Margin && nCnd < MaxCand; x++)
                    {
                        ref PatternBoard.Cell c = ref _pb.Cells[x, y];
                        if (c.Piece == PieceColor.Empty)
                        {
                            if (checkRenjuForbid && c.GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
                                continue;
                            cnd[nCnd++] = new Cand(x, y, 0);
                        }
                    }
                }
            }

            if (nCnd == 0) best.Value = 0; // Board is full draw
        }

        BoundType bound = BoundType.UpperBound;
        Point currentBestMove = Point.Empty;
        int nForbid = 0;

        for (int i = 0; i < nCnd; i++)
        {
            int cx = cnd[i].X;
            int cy = cnd[i].Y;

            // Renju check: Black cannot play on forbidden squares
            if (checkRenjuForbid && _pb.Cells[cx, cy].GetStatus4((int)_pb.Who) == CarbonTables.ThreatForbid)
            {
                nForbid++;
                continue;
            }

            _iterNodes++;
            _pb.MakePaddedMove(cx, cy);

            int vA = -beta;
            int vB = -(best.Value + 1);
            if (vB >= WinMin) vB++;
            if (vA <= -WinMin) vA--;

            Cand m = Minimax(h, vA, vB, cancellationToken, moveNowToken);
            int value = -m.Value;

            if (value >= WinMin) value--;
            if (value <= -WinMin) value++;

            _pb.UndoMove();

            cancellationToken.ThrowIfCancellationRequested();
            if (_terminate || moveNowToken.IsCancellationRequested)
            {
                _terminate = true;
                break;
            }

            if (value > best.Value)
            {
                best = new Cand(cx, cy, value);
                currentBestMove = new Point(cx - PatternBoard.Margin, cy - PatternBoard.Margin);

                if (value > beta)
                {
                    Table.Store(zobristKey, h, value, BoundType.LowerBound, currentBestMove);
                    return new Cand(cx, cy, beta + 1); // Closed-interval beta cutoff
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

