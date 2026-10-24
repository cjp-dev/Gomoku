using System.Runtime.CompilerServices;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// Encapsulates the 4-direction pattern bitboards, directional status lookups,
/// candidate bounding box, and threat tensors ported directly from Carbon Gomoku.
/// </summary>
public sealed class PatternBoard
{
    public const int MaxWidth = 64;
    public const int MaxHeight = 32;
    public const int Margin = 4;

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
        public sbyte Adj1, Adj2;

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
                CarbonTables.GetPrior(Pattern31, Pattern30) +
                (Adj1 != 0 ? 1 : 0));
        }
    }

    public Cell[,] Cells { get; } = new Cell[MaxWidth + 8, MaxHeight + 8];
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

    public int[,] NSt { get; } = new int[2, 10]; // nSt[player, grade]

    // Candidate bounding box (in padded coordinates 4..Width+3, 4..Height+3)
    public Point UpperLeftCand;
    public Point LowerRightCand;

    // History stacks
    private readonly Point[] _remMove = new Point[MaxWidth * MaxHeight + 64];
    private readonly Point[] _remULCand = new Point[MaxWidth * MaxHeight + 64];
    private readonly Point[] _remLRCand = new Point[MaxWidth * MaxHeight + 64];
    public readonly Point[] RemPlacedPoints = new Point[MaxWidth * MaxHeight + 64];

    public PatternBoard()
    {
    }

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

        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 10; j++)
                NSt[i, j] = 0;

        // Fill board with initial cells and margins
        for (int y = 0; y < height + 8; y++)
        {
            for (int x = 0; x < width + 8; x++)
            {
                bool isBorder = x < Margin || y < Margin || x >= width + Margin || y >= height + Margin;
                Cells[x, y] = default;
                Cells[x, y].Piece = isBorder ? PieceColor.Wrong : PieceColor.Empty;
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
                        if (Cells[xx, yy].Piece == PieceColor.Wrong)
                        {
                            Cells[x, y].SetPatternOr(k, 0, p);
                            Cells[x, y].SetPatternOr(k, 1, p);
                        }
                        xx -= DX[k]; yy -= DY[k];
                    }

                    xx = x + DX[k]; yy = y + DY[k];
                    for (byte p = 16; p != 0; p <<= 1)
                    {
                        if (Cells[xx, yy].Piece == PieceColor.Wrong)
                        {
                            Cells[x, y].SetPatternOr(k, 0, p);
                            Cells[x, y].SetPatternOr(k, 1, p);
                        }
                        xx += DX[k]; yy += DY[k];
                    }
                }
            }
        }

        bool exact5 = variant == GomokuVariant.Exact5;

        // Initialize status1 and status4
        for (int y = Margin; y < height + Margin; y++)
        {
            for (int x = Margin; x < width + Margin; x++)
            {
                Cells[x, y].Update1(0, exact5);
                Cells[x, y].Update1(1, exact5);
                Cells[x, y].Update1(2, exact5);
                Cells[x, y].Update1(3, exact5);
                Cells[x, y].Update4();
                Cells[x, y].Adj1 = 0;
                Cells[x, y].Adj2 = 0;
            }
        }

        UpperLeftCand = new Point(999, 999);
        LowerRightCand = new Point(0, 0);
    }

    /// <summary>
    /// Creates an isolated deep clone of the pattern board for parallel thread execution.
    /// </summary>
    public PatternBoard Clone()
    {
        PatternBoard clone = new()
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
            LowerRightCand = LowerRightCand
        };

        Array.Copy(Cells, clone.Cells, Cells.Length);
        Array.Copy(NSt, clone.NSt, NSt.Length);
        Array.Copy(_remMove, clone._remMove, _remMove.Length);
        Array.Copy(_remULCand, clone._remULCand, _remULCand.Length);
        Array.Copy(_remLRCand, clone._remLRCand, _remLRCand.Length);
        Array.Copy(RemPlacedPoints, clone.RemPlacedPoints, RemPlacedPoints.Length);

        return clone;
    }

    /// <summary>
    /// Executes move at board coordinate (xp, yp) in 0-based range [0..Width-1, 0..Height-1].
    /// </summary>
    public void MakeMove(int xp, int yp)
    {
        int xPadded = xp + Margin;
        int yPadded = yp + Margin;
        MakePaddedMove(xPadded, yPadded);
    }

    public void MakePaddedMove(int xp, int yp)
    {
        NSt[0, Cells[xp, yp].Status4_0]--;
        NSt[1, Cells[xp, yp].Status4_1]--;

        Cells[xp, yp].Piece = Who;
        ZobristHash ^= Zobrist.GetKey(Who, xp - Margin, yp - Margin);
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
                Cells[x, y].SetPatternOr(k, whoIdx, p);
                if (Cells[x, y].Piece == PieceColor.Empty)
                {
                    Cells[x, y].Update1(k, exact5);
                    NSt[0, Cells[x, y].Status4_0]--;
                    NSt[1, Cells[x, y].Status4_1]--;
                    Cells[x, y].Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    NSt[0, Cells[x, y].Status4_0]++;
                    NSt[1, Cells[x, y].Status4_1]++;
                }
            }

            x = xp; y = yp;
            for (byte p = 8; p != 0; p >>= 1)
            {
                x += DX[k]; y += DY[k];
                Cells[x, y].SetPatternOr(k, whoIdx, p);
                if (Cells[x, y].Piece == PieceColor.Empty)
                {
                    Cells[x, y].Update1(k, exact5);
                    NSt[0, Cells[x, y].Status4_0]--;
                    NSt[1, Cells[x, y].Status4_1]--;
                    Cells[x, y].Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    NSt[0, Cells[x, y].Status4_0]++;
                    NSt[1, Cells[x, y].Status4_1]++;
                }
            }
        }

        // Update candidate adjacency counters in 5x5 ring
        Cells[xp - 1, yp - 1].Adj1++; Cells[xp, yp - 1].Adj1++; Cells[xp + 1, yp - 1].Adj1++;
        Cells[xp - 1, yp    ].Adj1++;                           Cells[xp + 1, yp    ].Adj1++;
        Cells[xp - 1, yp + 1].Adj1++; Cells[xp, yp + 1].Adj1++; Cells[xp + 1, yp + 1].Adj1++;

        Cells[xp - 2, yp - 2].Adj2++; Cells[xp, yp - 2].Adj2++; Cells[xp + 2, yp - 2].Adj2++;
        Cells[xp - 2, yp    ].Adj2++;                           Cells[xp + 2, yp    ].Adj2++;
        Cells[xp - 2, yp + 2].Adj2++; Cells[xp, yp + 2].Adj2++; Cells[xp + 2, yp + 2].Adj2++;

        Who = Who.Opponent();
    }

    public void UndoMove()
    {
        MoveCount--;
        RemCount--;
        int xp = _remMove[MoveCount].X;
        int yp = _remMove[MoveCount].Y;
        UpperLeftCand = _remULCand[RemCount];
        LowerRightCand = _remLRCand[RemCount];

        bool exact5 = Variant == GomokuVariant.Exact5;
        Cells[xp, yp].Update1(0, exact5);
        Cells[xp, yp].Update1(1, exact5);
        Cells[xp, yp].Update1(2, exact5);
        Cells[xp, yp].Update1(3, exact5);
        Cells[xp, yp].Update4();
        if (Variant == GomokuVariant.Renju) CheckForbid(xp, yp);

        NSt[0, Cells[xp, yp].Status4_0]++;
        NSt[1, Cells[xp, yp].Status4_1]++;

        Cells[xp, yp].Piece = PieceColor.Empty;

        Who = Who.Opponent();
        ZobristHash ^= Zobrist.GetKey(Who, xp - Margin, yp - Margin);
        int whoIdx = (int)Who;

        for (int k = 0; k < 4; k++)
        {
            int x = xp; int y = yp;
            for (byte p = 16; p != 0; p <<= 1)
            {
                x -= DX[k]; y -= DY[k];
                Cells[x, y].SetPatternXor(k, whoIdx, p);
                if (Cells[x, y].Piece == PieceColor.Empty)
                {
                    Cells[x, y].Update1(k, exact5);
                    NSt[0, Cells[x, y].Status4_0]--;
                    NSt[1, Cells[x, y].Status4_1]--;
                    Cells[x, y].Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    NSt[0, Cells[x, y].Status4_0]++;
                    NSt[1, Cells[x, y].Status4_1]++;
                }
            }

            x = xp; y = yp;
            for (byte p = 8; p != 0; p >>= 1)
            {
                x += DX[k]; y += DY[k];
                Cells[x, y].SetPatternXor(k, whoIdx, p);
                if (Cells[x, y].Piece == PieceColor.Empty)
                {
                    Cells[x, y].Update1(k, exact5);
                    NSt[0, Cells[x, y].Status4_0]--;
                    NSt[1, Cells[x, y].Status4_1]--;
                    Cells[x, y].Update4();
                    if (Variant == GomokuVariant.Renju) CheckForbid(x, y);
                    NSt[0, Cells[x, y].Status4_0]++;
                    NSt[1, Cells[x, y].Status4_1]++;
                }
            }
        }

        Cells[xp - 1, yp - 1].Adj1--; Cells[xp, yp - 1].Adj1--; Cells[xp + 1, yp - 1].Adj1--;
        Cells[xp - 1, yp    ].Adj1--;                           Cells[xp + 1, yp    ].Adj1--;
        Cells[xp - 1, yp + 1].Adj1--; Cells[xp, yp + 1].Adj1--; Cells[xp + 1, yp + 1].Adj1--;

        Cells[xp - 2, yp - 2].Adj2--; Cells[xp, yp - 2].Adj2--; Cells[xp + 2, yp - 2].Adj2--;
        Cells[xp - 2, yp    ].Adj2--;                           Cells[xp + 2, yp    ].Adj2--;
        Cells[xp - 2, yp + 2].Adj2--; Cells[xp, yp + 2].Adj2--; Cells[xp + 2, yp + 2].Adj2--;
    }

    /// <summary>
    /// Exact port of AICarbon::checkForbid(x, y). Checks whether (x, y) is an illegal foul for FirstPlayer in Renju.
    /// </summary>
    public void CheckForbid(int x, int y)
    {
        int fp = (int)FirstPlayer;
        byte st4 = Cells[x, y].GetStatus4(fp);
        if (st4 < CarbonTables.ThreatF) return;

        if (st4 == CarbonTables.ThreatA)
        {
            int n6 = 0;
            for (int k = 0; k < 4; k++)
            {
                if (Cells[x, y].GetStatus1(k, fp) >= 9)
                {
                    int n = -1;
                    int x1 = x; int y1 = y;
                    do
                    {
                        x1 -= DX[k]; y1 -= DY[k];
                        n++;
                    } while (Cells[x1, y1].Piece == FirstPlayer);

                    x1 = x; y1 = y;
                    do
                    {
                        x1 += DX[k]; y1 += DY[k];
                        n++;
                    } while (Cells[x1, y1].Piece == FirstPlayer);

                    if (n >= 5)
                    {
                        if (n == 5) return; // Exact 5-in-a-row is valid and winning!
                        n6++;
                    }
                }
            }

            if (n6 > 0)
            {
                // Overline (6+) is a foul for FirstPlayer in Renju
                if (fp == 0) Cells[x, y].Status4_0 = CarbonTables.ThreatForbid;
                else Cells[x, y].Status4_1 = CarbonTables.ThreatForbid;
            }
            return;
        }

        int n3 = 0, n4 = 0;
        for (int k = 0; k < 4; k++)
        {
            int s = Cells[x, y].GetStatus1(k, fp);
            if (s >= 7) n4++;
            else if (s >= 6) n3++;
        }

        if (n4 > 1 || n3 > 1) // Double-four (4x4) or double-three (3x3)
        {
            if (fp == 0) Cells[x, y].Status4_0 = CarbonTables.ThreatForbid;
            else Cells[x, y].Status4_1 = CarbonTables.ThreatForbid;
        }
    }

    public bool IsForbidden(int xp, int yp, PieceColor player)
    {
        if (Variant != GomokuVariant.Renju || player != FirstPlayer) return false;
        int x = xp + Margin;
        int y = yp + Margin;
        return Cells[x, y].GetStatus4((int)player) == CarbonTables.ThreatForbid;
    }

    public bool CheckConsistency()
    {
        int[,] n = new int[2, 10];
        for (int y = UpperLeftCand.Y; y <= LowerRightCand.Y; y++)
        {
            for (int x = UpperLeftCand.X; x <= LowerRightCand.X; x++)
            {
                if (Cells[x, y].Piece == PieceColor.Empty && (Cells[x, y].Adj1 != 0 || Cells[x, y].Adj2 != 0))
                {
                    n[0, Cells[x, y].Status4_0]++;
                    n[1, Cells[x, y].Status4_1]++;
                }
            }
        }

        for (int p = 0; p < 2; p++)
        {
            for (int g = 1; g < 10; g++)
            {
                if (n[p, g] != NSt[p, g]) return false;
            }
        }

        return true;
    }
}

