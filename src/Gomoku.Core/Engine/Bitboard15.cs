using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// High-performance 16-bit SIMD bitboard representation of a 15x15 Gomoku board.
/// Each row, column, diagonal, and anti-diagonal is stored in a 16-bit word (ushort).
/// Supports instant O(1) win verification, Exact 5 detection, Renju overline fouls,
/// Zobrist hashing, and fast line threat scanning.
/// </summary>
public sealed class Bitboard15
{
    public const int Size = 15;
    public const int DiagCount = 29; // 2 * Size - 1
    public const ushort Mask15 = 0x7FFF; // 15 bits set

    // Bitboards for Black (0) and White (1)
    public readonly ushort[] Rows = new ushort[2 * Size];       // [player * Size + row]
    public readonly ushort[] Cols = new ushort[2 * Size];       // [player * Size + col]
    public readonly ushort[] Diag = new ushort[2 * DiagCount];  // [player * DiagCount + d]
    public readonly ushort[] Adiag = new ushort[2 * DiagCount]; // [player * DiagCount + ad]

    // Fast coordinate lookup tables
    private static readonly byte[] DiagIndex = new byte[Size * Size];
    private static readonly byte[] AdiagIndex = new byte[Size * Size];
    private static readonly byte[] DiagOffset = new byte[Size * Size];
    private static readonly byte[] AdiagOffset = new byte[Size * Size];
    private static readonly byte[] DiagLength = new byte[DiagCount];
    private static readonly byte[] AdiagLength = new byte[DiagCount];

    // Threat potential table: index = (1 << length) + mask
    // Unpacked via PDEP / ParallelBitDeposit
    private static readonly ulong[] PotentialThreatTable = new ulong[1 << 17];
    private static readonly ushort[] PotentialFiveMask = new ushort[1 << 17];
    private static readonly ushort[] PotentialFourOpenMask = new ushort[1 << 17];
    private static readonly ushort[] PotentialFourSimpleMask = new ushort[1 << 17];
    private static readonly (byte Start, byte Length)[] SplitSegments = new (byte, byte)[1 << 16 * 2]; // Left & Right per mask

    private static readonly ulong[,] ZobristKeys = new ulong[2, Size * Size];
    public static ulong WhiteToMoveKey { get; private set; }
    private static bool _tablesInitialized;
    private static readonly object InitLock = new();

    // Game state tracking
    private readonly (byte Row, byte Col)[] _history = new (byte, byte)[Size * Size];
    public int MoveCount { get; private set; }
    public PieceColor SideToMove { get; private set; } = PieceColor.Black;
    public ulong Hash { get; private set; }
    public bool HasWinner { get; private set; }

    static Bitboard15()
    {
        InitializeStaticTables();
    }

    public Bitboard15()
    {
        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RowIdx(int player, int row) => (player * Size) + row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ColIdx(int player, int col) => (player * Size) + col;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DiagIdx(int player, int d) => (player * DiagCount) + d;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AdiagIdx(int player, int ad) => (player * DiagCount) + ad;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Diag, int Offset, int Length) GetDiagCoord(int row, int col)
    {
        int idx = (row * Size) + col;
        int d = DiagIndex[idx];
        return (d, DiagOffset[idx], DiagLength[d]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Adiag, int Offset, int Length) GetAdiagCoord(int row, int col)
    {
        int idx = (row * Size) + col;
        int ad = AdiagIndex[idx];
        return (ad, AdiagOffset[idx], AdiagLength[ad]);
    }

    public void Clear()
    {
        Array.Clear(Rows);
        Array.Clear(Cols);
        Array.Clear(Diag);
        Array.Clear(Adiag);
        MoveCount = 0;
        SideToMove = PieceColor.Black;
        Hash = 0;
        HasWinner = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetZobristKey(int player, int posIdx) => ZobristKeys[player, posIdx];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHash(ulong hash) => Hash = hash;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetSideToMove(PieceColor side) => SideToMove = side;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMoveCount(int count) => MoveCount = count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PieceColor GetStone(int row, int col)
    {
        int bit = 1 << col;
        if ((Rows[RowIdx(0, row)] & bit) != 0) return PieceColor.Black;
        if ((Rows[RowIdx(1, row)] & bit) != 0) return PieceColor.White;
        return PieceColor.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty(int row, int col)
    {
        int bit = 1 << col;
        return ((Rows[RowIdx(0, row)] | Rows[RowIdx(1, row)]) & bit) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetOccupiedRow(int row) => (ushort)(Rows[RowIdx(0, row)] | Rows[RowIdx(1, row)]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetOccupiedCol(int col) => (ushort)(Cols[ColIdx(0, col)] | Cols[ColIdx(1, col)]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetOccupiedDiag(int d) => (ushort)(Diag[DiagIdx(0, d)] | Diag[DiagIdx(1, d)]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetOccupiedAdiag(int ad) => (ushort)(Adiag[AdiagIdx(0, ad)] | Adiag[AdiagIdx(1, ad)]);

    public void MakeMove(int row, int col)
    {
        int player = SideToMove == PieceColor.Black ? 0 : 1;
        int posIdx = (row * Size) + col;
        int d = DiagIndex[posIdx];
        int ad = AdiagIndex[posIdx];
        int dCol = DiagOffset[posIdx];
        int adCol = AdiagOffset[posIdx];

        Rows[RowIdx(player, row)] |= (ushort)(1 << col);
        Cols[ColIdx(player, col)] |= (ushort)(1 << row);
        Diag[DiagIdx(player, d)] |= (ushort)(1 << dCol);
        Adiag[AdiagIdx(player, ad)] |= (ushort)(1 << adCol);

        Hash ^= ZobristKeys[player, posIdx] ^ WhiteToMoveKey;
        _history[MoveCount++] = ((byte)row, (byte)col);

        // Win check
        if (CheckFiveAt(player, row, col, d, ad, dCol, adCol))
        {
            HasWinner = true;
        }

        SideToMove = SideToMove == PieceColor.Black ? PieceColor.White : PieceColor.Black;
    }

    public void UndoMove()
    {
        if (MoveCount == 0) return;

        MoveCount--;
        var (row, col) = _history[MoveCount];
        SideToMove = SideToMove == PieceColor.Black ? PieceColor.White : PieceColor.Black;
        int player = SideToMove == PieceColor.Black ? 0 : 1;

        int posIdx = (row * Size) + col;
        int d = DiagIndex[posIdx];
        int ad = AdiagIndex[posIdx];
        int dCol = DiagOffset[posIdx];
        int adCol = AdiagOffset[posIdx];

        Rows[RowIdx(player, row)] &= (ushort)~(1 << col);
        Cols[ColIdx(player, col)] &= (ushort)~(1 << row);
        Diag[DiagIdx(player, d)] &= (ushort)~(1 << dCol);
        Adiag[AdiagIdx(player, ad)] &= (ushort)~(1 << adCol);

        Hash ^= ZobristKeys[player, posIdx] ^ WhiteToMoveKey;
        HasWinner = false;
    }

    public Bitboard15 Clone()
    {
        Bitboard15 dst = new();
        Array.Copy(Rows, dst.Rows, Rows.Length);
        Array.Copy(Cols, dst.Cols, Cols.Length);
        Array.Copy(Diag, dst.Diag, Diag.Length);
        Array.Copy(Adiag, dst.Adiag, Adiag.Length);
        dst.MoveCount = MoveCount;
        dst.SideToMove = SideToMove;
        dst.Hash = Hash;
        dst.HasWinner = HasWinner;
        Array.Copy(_history, dst._history, MoveCount);
        return dst;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckFiveAt(int player, int row, int col, int d, int ad, int dCol, int adCol)
    {
        return HasFiveRun(Rows[RowIdx(player, row)]) ||
               HasFiveRun(Cols[ColIdx(player, col)]) ||
               HasFiveRun(Diag[DiagIdx(player, d)]) ||
               HasFiveRun(Adiag[AdiagIdx(player, ad)]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasFiveRun(ushort mask)
    {
        int m = mask;
        int m1 = m & (m >> 1);
        int m2 = m1 & (m1 >> 2);
        return (m2 & (m >> 4)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasExactFive(ushort mask)
    {
        int m = mask & Mask15;
        int m1 = m & (m >> 1);
        int m2 = m1 & (m1 >> 2);
        int fiveRuns = m2 & (m >> 4);
        if (fiveRuns == 0) return false;

        // Exact 5 cannot have adjacent stone at left (i-1) or right (i+5)
        int leftNeighbor = (m << 1) & Mask15;
        int rightNeighbor = (m >> 5);
        int exact = fiveRuns & ~leftNeighbor & ~rightNeighbor;
        return exact != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasOverline(ushort mask)
    {
        int m = mask & Mask15;
        int m1 = m & (m >> 1);
        int m2 = m1 & (m1 >> 2);
        return (m2 & (m >> 4) & (m >> 5)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Row, int Col) CoordFromDiag(int d, int i)
    {
        return d < Size - 1 ? (i, i + (Size - 1) - d) : (i + d - (Size - 1), i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Row, int Col) CoordFromAdiag(int ad, int i)
    {
        if (ad < Size - 1) return (i, ad - i);
        int c = Size - 1 - i;
        return (ad - c, c);
    }

    public int GetAllFiveMoves(int player, Span<Point> buffer)
    {
        int count = 0;
        Span<ushort> added = stackalloc ushort[Size];

        // Rows
        for (int r = 0; r < Size; r++)
        {
            ushort my = Rows[RowIdx(player, r)];
            ushort occ = GetOccupiedRow(r);
            for (int i = 0; i <= 10; i++)
            {
                int wMy = (my >> i) & 0x1F;
                int wOcc = (occ >> i) & 0x1F;
                if (wOcc == wMy && BitOperations.PopCount((uint)wMy) == 4)
                {
                    int c = i + BitOperations.TrailingZeroCount((uint)(wMy ^ 0x1F));
                    if ((added[r] & (1 << c)) == 0)
                    {
                        added[r] |= (ushort)(1 << c);
                        buffer[count++] = new Point(c, r);
                        if (count >= buffer.Length) return count;
                    }
                }
            }
        }

        // Cols
        for (int c = 0; c < Size; c++)
        {
            ushort my = Cols[ColIdx(player, c)];
            ushort occ = GetOccupiedCol(c);
            for (int i = 0; i <= 10; i++)
            {
                int wMy = (my >> i) & 0x1F;
                int wOcc = (occ >> i) & 0x1F;
                if (wOcc == wMy && BitOperations.PopCount((uint)wMy) == 4)
                {
                    int r = i + BitOperations.TrailingZeroCount((uint)(wMy ^ 0x1F));
                    if ((added[r] & (1 << c)) == 0)
                    {
                        added[r] |= (ushort)(1 << c);
                        buffer[count++] = new Point(c, r);
                        if (count >= buffer.Length) return count;
                    }
                }
            }
        }

        // Diag
        for (int d = 4; d <= 24; d++)
        {
            int len = DiagLength[d];
            ushort my = Diag[DiagIdx(player, d)];
            ushort occ = GetOccupiedDiag(d);
            for (int i = 0; i <= len - 5; i++)
            {
                int wMy = (my >> i) & 0x1F;
                int wOcc = (occ >> i) & 0x1F;
                if (wOcc == wMy && BitOperations.PopCount((uint)wMy) == 4)
                {
                    int offset = i + BitOperations.TrailingZeroCount((uint)(wMy ^ 0x1F));
                    var (r, c) = CoordFromDiag(d, offset);
                    if ((added[r] & (1 << c)) == 0)
                    {
                        added[r] |= (ushort)(1 << c);
                        buffer[count++] = new Point(c, r);
                        if (count >= buffer.Length) return count;
                    }
                }
            }
        }

        // Adiag
        for (int ad = 4; ad <= 24; ad++)
        {
            int len = AdiagLength[ad];
            ushort my = Adiag[AdiagIdx(player, ad)];
            ushort occ = GetOccupiedAdiag(ad);
            for (int i = 0; i <= len - 5; i++)
            {
                int wMy = (my >> i) & 0x1F;
                int wOcc = (occ >> i) & 0x1F;
                if (wOcc == wMy && BitOperations.PopCount((uint)wMy) == 4)
                {
                    int offset = i + BitOperations.TrailingZeroCount((uint)(wMy ^ 0x1F));
                    var (r, c) = CoordFromAdiag(ad, offset);
                    if ((added[r] & (1 << c)) == 0)
                    {
                        added[r] |= (ushort)(1 << c);
                        buffer[count++] = new Point(c, r);
                        if (count >= buffer.Length) return count;
                    }
                }
            }
        }

        return count;
    }

    public int GetAllOpenFourMoves(int player, Span<Point> buffer)
    {
        int count = 0;
        Span<ushort> added = stackalloc ushort[Size];

        // Rows
        for (int r = 0; r < Size; r++)
        {
            ushort my = Rows[RowIdx(player, r)];
            ushort occ = GetOccupiedRow(r);
            for (int i = 0; i <= 9; i++)
            {
                if (((occ >> i) & 1) == 0 && ((occ >> (i + 5)) & 1) == 0)
                {
                    int midMy = (my >> (i + 1)) & 0x0F;
                    int midOcc = (occ >> (i + 1)) & 0x0F;
                    if (midOcc == midMy && BitOperations.PopCount((uint)midMy) == 3)
                    {
                        int c = (i + 1) + BitOperations.TrailingZeroCount((uint)(midMy ^ 0x0F));
                        if ((added[r] & (1 << c)) == 0)
                        {
                            added[r] |= (ushort)(1 << c);
                            buffer[count++] = new Point(c, r);
                            if (count >= buffer.Length) return count;
                        }
                    }
                }
            }
        }

        // Cols
        for (int c = 0; c < Size; c++)
        {
            ushort my = Cols[ColIdx(player, c)];
            ushort occ = GetOccupiedCol(c);
            for (int i = 0; i <= 9; i++)
            {
                if (((occ >> i) & 1) == 0 && ((occ >> (i + 5)) & 1) == 0)
                {
                    int midMy = (my >> (i + 1)) & 0x0F;
                    int midOcc = (occ >> (i + 1)) & 0x0F;
                    if (midOcc == midMy && BitOperations.PopCount((uint)midMy) == 3)
                    {
                        int r = (i + 1) + BitOperations.TrailingZeroCount((uint)(midMy ^ 0x0F));
                        if ((added[r] & (1 << c)) == 0)
                        {
                            added[r] |= (ushort)(1 << c);
                            buffer[count++] = new Point(c, r);
                            if (count >= buffer.Length) return count;
                        }
                    }
                }
            }
        }

        // Diag
        for (int d = 5; d <= 23; d++)
        {
            int len = DiagLength[d];
            ushort my = Diag[DiagIdx(player, d)];
            ushort occ = GetOccupiedDiag(d);
            for (int i = 0; i <= len - 6; i++)
            {
                if (((occ >> i) & 1) == 0 && ((occ >> (i + 5)) & 1) == 0)
                {
                    int midMy = (my >> (i + 1)) & 0x0F;
                    int midOcc = (occ >> (i + 1)) & 0x0F;
                    if (midOcc == midMy && BitOperations.PopCount((uint)midMy) == 3)
                    {
                        int offset = (i + 1) + BitOperations.TrailingZeroCount((uint)(midMy ^ 0x0F));
                        var (r, c) = CoordFromDiag(d, offset);
                        if ((added[r] & (1 << c)) == 0)
                        {
                            added[r] |= (ushort)(1 << c);
                            buffer[count++] = new Point(c, r);
                            if (count >= buffer.Length) return count;
                        }
                    }
                }
            }
        }

        // Adiag
        for (int ad = 5; ad <= 23; ad++)
        {
            int len = AdiagLength[ad];
            ushort my = Adiag[AdiagIdx(player, ad)];
            ushort occ = GetOccupiedAdiag(ad);
            for (int i = 0; i <= len - 6; i++)
            {
                if (((occ >> i) & 1) == 0 && ((occ >> (i + 5)) & 1) == 0)
                {
                    int midMy = (my >> (i + 1)) & 0x0F;
                    int midOcc = (occ >> (i + 1)) & 0x0F;
                    if (midOcc == midMy && BitOperations.PopCount((uint)midMy) == 3)
                    {
                        int offset = (i + 1) + BitOperations.TrailingZeroCount((uint)(midMy ^ 0x0F));
                        var (r, c) = CoordFromAdiag(ad, offset);
                        if ((added[r] & (1 << c)) == 0)
                        {
                            added[r] |= (ushort)(1 << c);
                            buffer[count++] = new Point(c, r);
                            if (count >= buffer.Length) return count;
                        }
                    }
                }
            }
        }

        return count;
    }

    public Point FindWinningMove(int player)
    {
        Span<Point> buf = stackalloc Point[16];
        int nFives = GetAllFiveMoves(player, buf);
        if (nFives > 0) return buf[0];

        int nOpenFours = GetAllOpenFourMoves(player, buf);
        if (nOpenFours > 0) return buf[0];

        return Point.Empty;
    }

    public void GetFourDefenseMoves(int opponent, Span<Point> buffer, out int count)
    {
        count = GetAllFiveMoves(opponent, buffer);
        if (count > 0) return;

        count = GetAllOpenFourMoves(opponent, buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EvaluateLine(ushort my, ushort occ, int length, ref int fourThreats, ref int openThreeThreats)
    {
        int score = 0;
        // 5-windows
        for (int i = 0; i <= length - 5; i++)
        {
            int wMy = (my >> i) & 0x1F;
            int wOcc = (occ >> i) & 0x1F;
            if (wOcc == wMy) // No opponent stones
            {
                int c = BitOperations.PopCount((uint)wMy);
                switch (c)
                {
                    case 4:
                        score += 2500;
                        fourThreats++;
                        break;
                    case 3:
                        score += 500;
                        break;
                    case 2:
                        score += 50;
                        break;
                    case 1:
                        score += 5;
                        break;
                }
            }
        }

        // 6-windows
        for (int i = 0; i <= length - 6; i++)
        {
            if (((occ >> i) & 1) == 0 && ((occ >> (i + 5)) & 1) == 0)
            {
                int midMy = (my >> (i + 1)) & 0x0F;
                int midOcc = (occ >> (i + 1)) & 0x0F;
                if (midOcc == midMy)
                {
                    int midC = BitOperations.PopCount((uint)midMy);
                    switch (midC)
                    {
                        case 4:
                            score += 15000;
                            fourThreats += 2;
                            break;
                        case 3:
                            score += 1500;
                            openThreeThreats++;
                            break;
                        case 2:
                            score += 200;
                            break;
                    }
                }
            }
        }

        return score;
    }

    public int Evaluate()
    {
        int player = SideToMove == PieceColor.Black ? 0 : 1;
        int opp = 1 - player;

        int scorePlayer = 0;
        int scoreOpp = 0;
        int playerFours = 0, playerThrees = 0;
        int oppFours = 0, oppThrees = 0;

        // Rows
        for (int r = 0; r < Size; r++)
        {
            ushort occ = GetOccupiedRow(r);
            scorePlayer += EvaluateLine(Rows[RowIdx(player, r)], occ, Size, ref playerFours, ref playerThrees);
            scoreOpp += EvaluateLine(Rows[RowIdx(opp, r)], occ, Size, ref oppFours, ref oppThrees);
        }

        // Cols
        for (int c = 0; c < Size; c++)
        {
            ushort occ = GetOccupiedCol(c);
            scorePlayer += EvaluateLine(Cols[ColIdx(player, c)], occ, Size, ref playerFours, ref playerThrees);
            scoreOpp += EvaluateLine(Cols[ColIdx(opp, c)], occ, Size, ref oppFours, ref oppThrees);
        }

        // Diag
        for (int d = 4; d <= 24; d++)
        {
            int len = DiagLength[d];
            ushort occ = GetOccupiedDiag(d);
            scorePlayer += EvaluateLine(Diag[DiagIdx(player, d)], occ, len, ref playerFours, ref playerThrees);
            scoreOpp += EvaluateLine(Diag[DiagIdx(opp, d)], occ, len, ref oppFours, ref oppThrees);
        }

        // Adiag
        for (int ad = 4; ad <= 24; ad++)
        {
            int len = AdiagLength[ad];
            ushort occ = GetOccupiedAdiag(ad);
            scorePlayer += EvaluateLine(Adiag[AdiagIdx(player, ad)], occ, len, ref playerFours, ref playerThrees);
            scoreOpp += EvaluateLine(Adiag[AdiagIdx(opp, ad)], occ, len, ref oppFours, ref oppThrees);
        }

        // Fork bonuses
        if (playerFours >= 2) scorePlayer += 20000;
        else if (playerFours >= 1 && playerThrees >= 1) scorePlayer += 15000;
        else if (playerThrees >= 2) scorePlayer += 10000;

        if (oppFours >= 2) scoreOpp += 20000;
        else if (oppFours >= 1 && oppThrees >= 1) scoreOpp += 15000;
        else if (oppThrees >= 2) scoreOpp += 10000;

        return scorePlayer - scoreOpp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ParallelBitDeposit(ulong value, ulong mask)
    {
        if (Bmi2.X64.IsSupported)
        {
            return Bmi2.X64.ParallelBitDeposit(value, mask);
        }

        ulong result = 0;
        int valueBit = 0;
        while (mask != 0)
        {
            ulong lowestBit = mask & (0 - mask);
            if (((value >> valueBit) & 1) != 0)
            {
                result |= lowestBit;
            }
            mask &= mask - 1;
            valueBit++;
        }
        return result;
    }

    private static void InitializeStaticTables()
    {
        lock (InitLock)
        {
            if (_tablesInitialized) return;

            // 1. Initialize Diagonals & Anti-Diagonals
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    int idx = (r * Size) + c;
                    int d = r - c + (Size - 1);
                    int ad = r + c;

                    DiagIndex[idx] = (byte)d;
                    AdiagIndex[idx] = (byte)ad;
                    DiagOffset[idx] = (byte)Math.Min(r, c);
                    AdiagOffset[idx] = (byte)Math.Min(r, Size - 1 - c);
                }
            }

            for (int d = 0; d < DiagCount; d++)
            {
                DiagLength[d] = (byte)(Size - Math.Abs(d - (Size - 1)));
                AdiagLength[d] = (byte)(Size - Math.Abs(d - (Size - 1)));
            }

            // 2. Initialize Zobrist Keys
            Random rng = new(424242);
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < Size * Size; i++)
                {
                    byte[] bytes = new byte[8];
                    rng.NextBytes(bytes);
                    ZobristKeys[p, i] = BitConverter.ToUInt64(bytes, 0);
                }
            }
            byte[] whiteBytes = new byte[8];
            rng.NextBytes(whiteBytes);
            WhiteToMoveKey = BitConverter.ToUInt64(whiteBytes, 0);

            // 3. Initialize Potential Threat Table
            for (int len = 5; len <= Size; len++)
            {
                int maxMask = 1 << len;
                for (int mask = 0; mask < maxMask; mask++)
                {
                    ulong pot = 0;
                    ushort fiveM = 0;
                    ushort fourOpenM = 0;
                    ushort fourSimpleM = 0;

                    for (int i = 0; i < len; i++)
                    {
                        if (((mask >> i) & 1) != 0) continue;
                        int toggled = mask | (1 << i);
                        int severity = 0;
                        int ways = 0;

                        int startJ = Math.Max(i - 4, 0);
                        int endJ = Math.Min(i, len - 5);
                        for (int j = startJ; j <= endJ; j++)
                        {
                            int c = BitOperations.PopCount((uint)((toggled >> j) & 0x1F));
                            if (c > severity)
                            {
                                severity = c;
                                ways = 1;
                            }
                            else if (c == severity)
                            {
                                ways++;
                            }
                        }

                        byte threat = ClassifyThreat(severity, ways);
                        pot |= ((ulong)threat) << (i * 4);

                        if (threat == 15) fiveM |= (ushort)(1 << i);
                        else if (threat == 14) fourOpenM |= (ushort)(1 << i);
                        else if (threat == 13) fourSimpleM |= (ushort)(1 << i);
                    }

                    int tableIdx = (1 << len) + mask;
                    PotentialThreatTable[tableIdx] = pot;
                    PotentialFiveMask[tableIdx] = fiveM;
                    PotentialFourOpenMask[tableIdx] = fourOpenM;
                    PotentialFourSimpleMask[tableIdx] = fourSimpleM;
                }
            }

            _tablesInitialized = true;
        }
    }

    private static byte ClassifyThreat(int stonesInFive, int ways)
    {
        return stonesInFive switch
        {
            5 => 15, // Five
            4 => (byte)(ways >= 2 ? 14 : 13), // Four: open (14) vs simple (13)
            3 => (byte)(ways >= 2 ? 12 : 10), // Three: open (12) vs simple (10)
            2 => (byte)(ways >= 2 ? 9 : 6),   // Two: open (9) vs simple (6)
            1 => (byte)(ways >= 2 ? 5 : 1),   // One: open (5) vs simple (1)
            _ => 0
        };
    }
}
