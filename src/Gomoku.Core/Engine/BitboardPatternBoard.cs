using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// High-performance PatternBoard optimized with 1D contiguous memory layout,
/// power-of-two bitwise indexing ((y &lt;&lt; 7) | x), and bitboard Chebyshev dilation
/// for candidate generation (eliminating 24 adjacency counter writes per make/undo).
/// </summary>
public sealed class BitboardPatternBoard
{
    public const int MaxWidth = 64;
    public const int MaxHeight = 32;
    public const int Margin = 4;
    public const int Stride = 128; // Power of two stride for (y << 7) | x

    public static readonly int[] DX = [1, 0, 1, 1];
    public static readonly int[] DY = [0, 1, 1, -1];

    public struct Cell
    {
        public PieceColor Piece;
        public byte Pattern00, Pattern01;
        public byte Pattern10, Pattern11;
        public byte Pattern20, Pattern21;
        public byte Pattern30, Pattern31;

        public byte Status1_00, Status1_01;
        public byte Status1_10, Status1_11;
        public byte Status1_20, Status1_21;
        public byte Status1_30, Status1_31;

        public byte Status4_0, Status4_1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetPattern(int k, int player) => k switch
        {
            0 => player == 0 ? Pattern00 : Pattern01,
            1 => player == 0 ? Pattern10 : Pattern11,
            2 => player == 0 ? Pattern20 : Pattern21,
            _ => player == 0 ? Pattern30 : Pattern31,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPatternOr(int k, int player, byte p)
        {
            if (k == 0) { if (player == 0) Pattern00 |= p; else Pattern01 |= p; }
            else if (k == 1) { if (player == 0) Pattern10 |= p; else Pattern11 |= p; }
            else if (k == 2) { if (player == 0) Pattern20 |= p; else Pattern21 |= p; }
            else { if (player == 0) Pattern30 |= p; else Pattern31 |= p; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPatternXor(int k, int player, byte p)
        {
            if (k == 0) { if (player == 0) Pattern00 ^= p; else Pattern01 ^= p; }
            else if (k == 1) { if (player == 0) Pattern10 ^= p; else Pattern11 ^= p; }
            else if (k == 2) { if (player == 0) Pattern20 ^= p; else Pattern21 ^= p; }
            else { if (player == 0) Pattern30 ^= p; else Pattern31 ^= p; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetStatus1(int k, int player) => k switch
        {
            0 => player == 0 ? Status1_00 : Status1_01,
            1 => player == 0 ? Status1_10 : Status1_11,
            2 => player == 0 ? Status1_20 : Status1_21,
            _ => player == 0 ? Status1_30 : Status1_31,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update1(int k, bool exact5 = false)
        {
            if (k == 0)
            {
                Status1_00 = CarbonTables.GetStatus1(Pattern00, Pattern01, exact5);
                Status1_01 = CarbonTables.GetStatus1(Pattern01, Pattern00, exact5);
            }
            else if (k == 1)
            {
                Status1_10 = CarbonTables.GetStatus1(Pattern10, Pattern11, exact5);
                Status1_11 = CarbonTables.GetStatus1(Pattern11, Pattern10, exact5);
            }
            else if (k == 2)
            {
                Status1_20 = CarbonTables.GetStatus1(Pattern20, Pattern21, exact5);
                Status1_21 = CarbonTables.GetStatus1(Pattern21, Pattern20, exact5);
            }
            else
            {
                Status1_30 = CarbonTables.GetStatus1(Pattern30, Pattern31, exact5);
                Status1_31 = CarbonTables.GetStatus1(Pattern31, Pattern30, exact5);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update4()
        {
            Status4_0 = CarbonTables.GetStatus4(Status1_00, Status1_10, Status1_20, Status1_30);
            Status4_1 = CarbonTables.GetStatus4(Status1_01, Status1_11, Status1_21, Status1_31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetStatus4(int player) => player == 0 ? Status4_0 : Status4_1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short Prior()
        {
            return (short)(
                CarbonTables.GetPrior(Pattern00, Pattern01) +
                CarbonTables.GetPrior(Pattern10, Pattern11) +
                CarbonTables.GetPrior(Pattern20, Pattern21) +
                CarbonTables.GetPrior(Pattern30, Pattern31) +
                CarbonTables.GetPrior(Pattern01, Pattern00) +
                CarbonTables.GetPrior(Pattern11, Pattern10) +
                CarbonTables.GetPrior(Pattern21, Pattern20) +
                CarbonTables.GetPrior(Pattern31, Pattern30) + 1);
        }
    }

    public readonly Cell[] Cells = new Cell[Stride * (MaxHeight + 8)];
    public readonly int[] NSt = new int[32]; // Flat (player << 4) | grade
    public readonly uint[] Occupied = new uint[MaxHeight + 8];

    /// <summary>
    /// Running sum of Carbon's stone score (Rank of each stone's pattern in each direction) per colour,
    /// kept up to date by make/undo so the evaluation needs no loop over the stones.
    /// </summary>
    public readonly int[] StoneScore = new int[2];

    public int BoardWidth { get; private set; }
    public int BoardHeight { get; private set; }
    public GomokuVariant Variant { get; private set; }
    public PieceColor Who { get; internal set; }
    public PieceColor Opp => Who.Opponent();
    public PieceColor FirstPlayer { get; private set; }
    public int MoveCount { get; private set; }
    public int RemCount { get; private set; }
    public ulong ZobristHash { get; private set; }
    public ulong ZobristKey => Who == PieceColor.White ? ZobristHash ^ Zobrist.SideToMoveKey : ZobristHash;

    public Point UpperLeftCand;
    public Point LowerRightCand;

    private readonly Point[] _remMove = new Point[MaxWidth * MaxHeight + 64];
    private readonly Point[] _remULCand = new Point[MaxWidth * MaxHeight + 64];
    private readonly Point[] _remLRCand = new Point[MaxWidth * MaxHeight + 64];
    public readonly Point[] RemPlacedPoints = new Point[MaxWidth * MaxHeight + 64];

    // Undo journal: before each move, the 33 cells it changes (the played cell and 4 x 8 neighbours)
    // and the threat counters, so UndoMove copies them back instead of recomputing the patterns.
    // Moves made before _journalBase (e.g. before Clone) have no entry and are undone by recomputing.
    private const int JournalCells = 33;
    private Cell[] _jCells = new Cell[64 * JournalCells];
    private int[] _jNSt = new int[64 * 32];
    private int[] _jScore = new int[64 * 2];
    private int _journalBase;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Cell GetCell(int x, int y) => ref Cells[(y << 7) | x];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref int GetNSt(int player, int grade) => ref NSt[(player << 4) | grade];

    /// <summary>Carbon's score of a stone of colour <paramref name="a"/> in direction <paramref name="k"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirScore(ref Cell c, int k, int a) =>
        CarbonTables.Rank[CarbonTables.GetConfig(c.GetPattern(k, a), c.GetPattern(k, 1 - a))];

    /// <summary>Applies a pattern change to a cell and keeps <see cref="StoneScore"/> up to date if it holds a stone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChangePattern(ref Cell c, int k, int player, byte p, bool set)
    {
        int a = (int)c.Piece;
        if (a <= 1)
        {
            StoneScore[a] -= DirScore(ref c, k, a);
            if (set) c.SetPatternOr(k, player, p); else c.SetPatternXor(k, player, p);
            StoneScore[a] += DirScore(ref c, k, a);
        }
        else if (set) c.SetPatternOr(k, player, p);
        else c.SetPatternXor(k, player, p);
    }

    /// <summary>Carbon's evaluation from the side to move: own stone score minus the opponent's.</summary>
    public int Evaluate() => Who == PieceColor.Black ? StoneScore[0] - StoneScore[1] : StoneScore[1] - StoneScore[0];

    public void Start(int width, int height, GomokuVariant variant = GomokuVariant.Freestyle, PieceColor firstPlayer = PieceColor.Black)
    {
        BoardWidth = width;
        BoardHeight = height;
        Variant = variant;
        FirstPlayer = firstPlayer;
        Who = firstPlayer;
        MoveCount = 0;
        RemCount = 0;
        ZobristHash = 0UL;
        _journalBase = 0;

        Array.Clear(NSt);
        Array.Clear(Occupied);
        Array.Clear(StoneScore);

        // Fill board with initial cells and margins
        for (int y = 0; y < height + 8; y++)
        {
            for (int x = 0; x < width + 8; x++)
            {
                bool isBorder = x < Margin || y < Margin || x >= width + Margin || y >= height + Margin;
                ref Cell c = ref GetCell(x, y);
                c = default;
                c.Piece = isBorder ? PieceColor.Wrong : PieceColor.Empty;
            }
        }

        // Initialize pattern bits for board edge borders
        for (int y = Margin; y < height + Margin; y++)
        {
            for (int x = Margin; x < width + Margin; x++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int xx = x - DX[k]; int yy = y - DY[k];
                    for (byte p = 8; p != 0; p >>= 1)
                    {
                        if (GetCell(xx, yy).Piece == PieceColor.Wrong)
                        {
                            GetCell(x, y).SetPatternOr(k, 0, p);
                            GetCell(x, y).SetPatternOr(k, 1, p);
                        }
                        xx -= DX[k]; yy -= DY[k];
                    }

                    xx = x + DX[k]; yy = y + DY[k];
                    for (byte p = 16; p != 0; p <<= 1)
                    {
                        if (GetCell(xx, yy).Piece == PieceColor.Wrong)
                        {
                            GetCell(x, y).SetPatternOr(k, 0, p);
                            GetCell(x, y).SetPatternOr(k, 1, p);
                        }
                        xx += DX[k]; yy += DY[k];
                    }
                }
            }
        }

        bool exact5 = variant == GomokuVariant.Exact5;

        for (int y = Margin; y < height + Margin; y++)
        {
            for (int x = Margin; x < width + Margin; x++)
            {
                ref Cell c = ref GetCell(x, y);
                c.Update1(0, exact5);
                c.Update1(1, exact5);
                c.Update1(2, exact5);
                c.Update1(3, exact5);
                c.Update4();
            }
        }

        UpperLeftCand = new Point(999, 999);
        LowerRightCand = new Point(0, 0);
    }

    public BitboardPatternBoard Clone()
    {
        BitboardPatternBoard clone = new()
        {
            BoardWidth = BoardWidth,
            BoardHeight = BoardHeight,
            Variant = Variant,
            Who = Who,
            FirstPlayer = FirstPlayer,
            MoveCount = MoveCount,
            RemCount = RemCount,
            ZobristHash = ZobristHash,
            UpperLeftCand = UpperLeftCand,
            LowerRightCand = LowerRightCand,
            _journalBase = MoveCount
        };

        Array.Copy(Cells, clone.Cells, Cells.Length);
        Array.Copy(NSt, clone.NSt, NSt.Length);
        Array.Copy(Occupied, clone.Occupied, Occupied.Length);
        Array.Copy(StoneScore, clone.StoneScore, StoneScore.Length);
        Array.Copy(_remMove, clone._remMove, _remMove.Length);
        Array.Copy(_remULCand, clone._remULCand, _remULCand.Length);
        Array.Copy(_remLRCand, clone._remLRCand, _remLRCand.Length);
        Array.Copy(RemPlacedPoints, clone.RemPlacedPoints, RemPlacedPoints.Length);

        return clone;
    }

    public void MakeMove(int xp, int yp)
    {
        MakePaddedMove(xp + Margin, yp + Margin);
    }

    public void MakePaddedMove(int xp, int yp)
    {
        SaveJournal(xp, yp);
        ref Cell playedCell = ref GetCell(xp, yp);
        GetNSt(0, playedCell.Status4_0)--;
        GetNSt(1, playedCell.Status4_1)--;

        playedCell.Piece = Who;
        int whoScore = (int)Who;
        for (int k = 0; k < 4; k++)
        {
            StoneScore[whoScore] += DirScore(ref playedCell, k, whoScore);
        }
        int bx = xp - Margin;
        int by = yp - Margin;
        Occupied[by] |= (1u << bx);

        ZobristHash ^= Zobrist.GetKey(Who, bx, by);
        _remMove[MoveCount] = new Point(xp, yp);
        RemPlacedPoints[RemCount] = new Point(xp, yp);
        _remULCand[RemCount] = UpperLeftCand;
        _remLRCand[RemCount] = LowerRightCand;
        MoveCount++;
        RemCount++;

        if (xp - 2 < UpperLeftCand.X) UpperLeftCand = new Point(Math.Max(xp - 2, Margin), UpperLeftCand.Y);
        if (yp - 2 < UpperLeftCand.Y) UpperLeftCand = new Point(UpperLeftCand.X, Math.Max(yp - 2, Margin));
        if (xp + 2 > LowerRightCand.X) LowerRightCand = new Point(Math.Min(xp + 2, BoardWidth + Margin - 1), LowerRightCand.Y);
        if (yp + 2 > LowerRightCand.Y) LowerRightCand = new Point(LowerRightCand.X, Math.Min(yp + 2, BoardHeight + Margin - 1));

        bool exact5 = Variant == GomokuVariant.Exact5;
        int whoIdx = (int)Who;

        for (int k = 0; k < 4; k++)
        {
            int x = xp; int y = yp;
            for (byte p = 16; p != 0; p <<= 1)
            {
                x -= DX[k]; y -= DY[k];
                ref Cell c = ref GetCell(x, y);
                ChangePattern(ref c, k, whoIdx, p, set: true);
                if (c.Piece == PieceColor.Empty)
                {
                    c.Update1(k, exact5);
                    GetNSt(0, c.Status4_0)--;
                    GetNSt(1, c.Status4_1)--;
                    c.Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    GetNSt(0, c.Status4_0)++;
                    GetNSt(1, c.Status4_1)++;
                }
            }

            x = xp; y = yp;
            for (byte p = 8; p != 0; p >>= 1)
            {
                x += DX[k]; y += DY[k];
                ref Cell c = ref GetCell(x, y);
                ChangePattern(ref c, k, whoIdx, p, set: true);
                if (c.Piece == PieceColor.Empty)
                {
                    c.Update1(k, exact5);
                    GetNSt(0, c.Status4_0)--;
                    GetNSt(1, c.Status4_1)--;
                    c.Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    GetNSt(0, c.Status4_0)++;
                    GetNSt(1, c.Status4_1)++;
                }
            }
        }

        Who = Who.Opponent();
    }

    private void SaveJournal(int xp, int yp)
    {
        int slot = MoveCount - _journalBase;
        if (slot < 0) return;
        if ((slot + 1) * JournalCells > _jCells.Length)
        {
            Array.Resize(ref _jCells, _jCells.Length * 2);
            Array.Resize(ref _jNSt, _jNSt.Length * 2);
            Array.Resize(ref _jScore, _jScore.Length * 2);
        }

        ref Cell cells = ref MemoryMarshal.GetArrayDataReference(Cells);
        ref Cell j = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_jCells), slot * JournalCells);
        int idx = (yp << 7) | xp;
        j = Unsafe.Add(ref cells, idx);
        int n = 1;
        for (int k = 0; k < 4; k++)
        {
            int d = Delta[k];
            for (int i = 1; i <= 4; i++)
            {
                Unsafe.Add(ref j, n++) = Unsafe.Add(ref cells, idx - i * d);
                Unsafe.Add(ref j, n++) = Unsafe.Add(ref cells, idx + i * d);
            }
        }

        NSt.AsSpan().CopyTo(_jNSt.AsSpan(slot * 32, 32));
        _jScore[2 * slot] = StoneScore[0];
        _jScore[2 * slot + 1] = StoneScore[1];
    }

    private bool RestoreJournal(int xp, int yp)
    {
        int slot = MoveCount - _journalBase;
        if (slot < 0) return false;

        ref Cell cells = ref MemoryMarshal.GetArrayDataReference(Cells);
        ref Cell j = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_jCells), slot * JournalCells);
        int idx = (yp << 7) | xp;
        Unsafe.Add(ref cells, idx) = j;
        int n = 1;
        for (int k = 0; k < 4; k++)
        {
            int d = Delta[k];
            for (int i = 1; i <= 4; i++)
            {
                Unsafe.Add(ref cells, idx - i * d) = Unsafe.Add(ref j, n++);
                Unsafe.Add(ref cells, idx + i * d) = Unsafe.Add(ref j, n++);
            }
        }

        _jNSt.AsSpan(slot * 32, 32).CopyTo(NSt);
        StoneScore[0] = _jScore[2 * slot];
        StoneScore[1] = _jScore[2 * slot + 1];
        return true;
    }

    // Index step of direction k in Cells: (DX[k], DY[k]) with the stride of 128.
    private static ReadOnlySpan<int> Delta => [1, Stride, Stride + 1, 1 - Stride];

    public void UndoMove()
    {
        MoveCount--;
        RemCount--;
        int xp = _remMove[MoveCount].X;
        int yp = _remMove[MoveCount].Y;
        UpperLeftCand = _remULCand[RemCount];
        LowerRightCand = _remLRCand[RemCount];

        int bx = xp - Margin;
        int by = yp - Margin;
        Occupied[by] ^= (1u << bx);

        if (RestoreJournal(xp, yp))
        {
            Who = Who.Opponent();
            ZobristHash ^= Zobrist.GetKey(Who, bx, by);
            return;
        }

        bool exact5 = Variant == GomokuVariant.Exact5;
        ref Cell cell = ref GetCell(xp, yp);
        cell.Update1(0, exact5);
        cell.Update1(1, exact5);
        cell.Update1(2, exact5);
        cell.Update1(3, exact5);
        cell.Update4();
        if (Variant == GomokuVariant.Renju) CheckForbid(xp, yp);

        GetNSt(0, cell.Status4_0)++;
        GetNSt(1, cell.Status4_1)++;

        int stone = (int)cell.Piece;
        for (int k = 0; k < 4; k++)
        {
            StoneScore[stone] -= DirScore(ref cell, k, stone);
        }
        cell.Piece = PieceColor.Empty;

        Who = Who.Opponent();
        ZobristHash ^= Zobrist.GetKey(Who, bx, by);
        int whoIdx = (int)Who;

        for (int k = 0; k < 4; k++)
        {
            int x = xp; int y = yp;
            for (byte p = 16; p != 0; p <<= 1)
            {
                x -= DX[k]; y -= DY[k];
                ref Cell c = ref GetCell(x, y);
                ChangePattern(ref c, k, whoIdx, p, set: false);
                if (c.Piece == PieceColor.Empty)
                {
                    c.Update1(k, exact5);
                    GetNSt(0, c.Status4_0)--;
                    GetNSt(1, c.Status4_1)--;
                    c.Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    GetNSt(0, c.Status4_0)++;
                    GetNSt(1, c.Status4_1)++;
                }
            }

            x = xp; y = yp;
            for (byte p = 8; p != 0; p >>= 1)
            {
                x += DX[k]; y += DY[k];
                ref Cell c = ref GetCell(x, y);
                ChangePattern(ref c, k, whoIdx, p, set: false);
                if (c.Piece == PieceColor.Empty)
                {
                    c.Update1(k, exact5);
                    GetNSt(0, c.Status4_0)--;
                    GetNSt(1, c.Status4_1)--;
                    c.Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    GetNSt(0, c.Status4_0)++;
                    GetNSt(1, c.Status4_1)++;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint DilateRow(uint m, uint boardMask)
    {
        uint d1 = (m | (m << 1) | (m >> 1)) & boardMask;
        return (d1 | (d1 << 1) | (d1 >> 1)) & boardMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetCandidateMask(int by, ReadOnlySpan<uint> dilatedRows, uint boardMask)
    {
        uint mask = 0;
        int y0 = Math.Max(0, by - 2);
        int y1 = Math.Min(BoardHeight - 1, by + 2);
        for (int r = y0; r <= y1; r++)
        {
            mask |= dilatedRows[r];
        }
        return mask & ~Occupied[by] & boardMask;
    }

    public void CheckForbid(int x, int y)
    {
        int fp = (int)FirstPlayer;
        ref Cell cell = ref GetCell(x, y);
        byte st4 = cell.GetStatus4(fp);
        if (st4 < CarbonTables.ThreatF) return;

        if (st4 == CarbonTables.ThreatA)
        {
            int n6 = 0;
            for (int k = 0; k < 4; k++)
            {
                if (cell.GetStatus1(k, fp) >= 9)
                {
                    int n = -1;
                    int x1 = x; int y1 = y;
                    do
                    {
                        x1 -= DX[k]; y1 -= DY[k];
                        n++;
                    } while (GetCell(x1, y1).Piece == FirstPlayer);

                    x1 = x; y1 = y;
                    do
                    {
                        x1 += DX[k]; y1 += DY[k];
                        n++;
                    } while (GetCell(x1, y1).Piece == FirstPlayer);

                    if (n >= 5)
                    {
                        if (n == 5) return;
                        n6++;
                    }
                }
            }

            if (n6 > 0)
            {
                if (fp == 0) cell.Status4_0 = CarbonTables.ThreatForbid;
                else cell.Status4_1 = CarbonTables.ThreatForbid;
            }
            return;
        }

        int n3 = 0, n4 = 0;
        for (int k = 0; k < 4; k++)
        {
            int s = cell.GetStatus1(k, fp);
            if (s >= 7) n4++;
            else if (s >= 6) n3++;
        }

        if (n4 > 1 || n3 > 1)
        {
            if (fp == 0) cell.Status4_0 = CarbonTables.ThreatForbid;
            else cell.Status4_1 = CarbonTables.ThreatForbid;
        }
    }

    public bool IsForbidden(int xp, int yp, PieceColor player)
    {
        if (Variant != GomokuVariant.Renju || player != FirstPlayer) return false;
        return GetCell(xp + Margin, yp + Margin).GetStatus4((int)player) == CarbonTables.ThreatForbid;
    }

    public bool CheckConsistency()
    {
        int[] score = new int[2];
        for (int i = 0; i < RemCount; i++)
        {
            ref Cell s = ref GetCell(RemPlacedPoints[i].X, RemPlacedPoints[i].Y);
            int a = (int)s.Piece;
            for (int k = 0; k < 4; k++) score[a] += DirScore(ref s, k, a);
        }
        if (score[0] != StoneScore[0] || score[1] != StoneScore[1]) return false;

        for (int y = Margin; y < BoardHeight + Margin; y++)
        {
            for (int x = Margin; x < BoardWidth + Margin; x++)
            {
                ref Cell c = ref GetCell(x, y);
                if (c.Piece != PieceColor.Empty) continue;
                for (int p = 0; p < 2; p++)
                {
                    byte s0 = CarbonTables.GetStatus1(c.GetPattern(0, p), c.GetPattern(0, 1 - p), Variant == GomokuVariant.Exact5);
                    byte s1 = CarbonTables.GetStatus1(c.GetPattern(1, p), c.GetPattern(1, 1 - p), Variant == GomokuVariant.Exact5);
                    byte s2 = CarbonTables.GetStatus1(c.GetPattern(2, p), c.GetPattern(2, 1 - p), Variant == GomokuVariant.Exact5);
                    byte s3 = CarbonTables.GetStatus1(c.GetPattern(3, p), c.GetPattern(3, 1 - p), Variant == GomokuVariant.Exact5);
                    byte expected = CarbonTables.GetStatus4(s0, s1, s2, s3);
                    if (c.GetStatus4(p) != expected && c.GetStatus4(p) != CarbonTables.ThreatForbid)
                        return false;
                }
            }
        }
        return true;
    }
}

