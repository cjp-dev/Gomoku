using System.Runtime.CompilerServices;

namespace Gomoku.Core.AI;

/// <summary>
/// Precomputed lookup tables and pattern evaluation matrices ported directly from Carbon Gomoku.
/// </summary>
public static partial class CarbonTables
{
    public const byte ThreatNone = 0;
    public const byte ThreatH = 1;
    public const byte ThreatG = 2;
    public const byte ThreatF = 3;
    public const byte ThreatE = 4;
    public const byte ThreatD = 5;
    public const byte ThreatC = 6;
    public const byte ThreatB = 7;
    public const byte ThreatA = 8;
    public const byte ThreatForbid = 9;

    public static readonly int[] Rank = new int[107];
    public static readonly byte[] Status4 = new byte[10 * 10 * 10 * 10];
    public static readonly byte[] Status1Exact5Flat = new byte[256 * 256];

    static CarbonTables()
    {
        // 1. Compute Rank[cfg]
        for (int cfg = 0; cfg < 107; cfg++)
        {
            Rank[cfg] =
                19 * Count5(cfg, 4) +
                15 * Count5(cfg, 3) +
                11 * Count5(cfg, 2) +
                 7 * Count5(cfg, 1) +
                 3 * Count5(cfg, 0);
        }

        // 2. Compute Status4 tensor (10x10x10x10)
        for (int a = 0; a < 10; a++)
            for (int b = 0; b < 10; b++)
                for (int c = 0; c < 10; c++)
                    for (int d = 0; d < 10; d++)
                    {
                        Status4[((a * 10 + b) * 10 + c) * 10 + d] = ComputeStatus4((byte)a, (byte)b, (byte)c, (byte)d);
                    }

        // 3. Compute Status1Exact5Flat from Status1StandardFlat
        Array.Copy(Status1StandardFlat, Status1Exact5Flat, Status1StandardFlat.Length);
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                uint v = 0xf8u | (uint)j;
                int a = (int)(((v >> i) | (v << (8 - i))) & 0xff);
                for (int k = 0; k < 8; k++)
                {
                    int b = (int)(((k >> i) | (k << (8 - i))) & 0xff);
                    Status1Exact5Flat[(a << 8) | b] = 0;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Count5(int cfg, int move) => Count5Flat[cfg * 5 + move];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetConfig(byte me, byte opp) => ConfigFlat[(me << 8) | opp];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetPrior(byte me, byte opp) => PriorFlat[(me << 8) | opp];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetStatus1(byte me, byte opp, bool exact5 = false) =>
        exact5 ? Status1Exact5Flat[(me << 8) | opp] : Status1StandardFlat[(me << 8) | opp];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetStatus4(byte s0, byte s1, byte s2, byte s3) =>
        Status4[((s0 * 10 + s1) * 10 + s2) * 10 + s3];

    /// <summary>
    /// Exact implementation of AICarbon::getStatus4(s0, s1, s2, s3).
    /// </summary>
    private static byte ComputeStatus4(byte s0, byte s1, byte s2, byte s3)
    {
        Span<int> n = stackalloc int[10];
        n[s0]++; n[s1]++; n[s2]++; n[s3]++;

        if (n[9] >= 1) return ThreatA;              // OOOO_ (immediate win)
        if (n[8] >= 1) return ThreatB;              // OOO_ (unstoppable open four)
        if (n[7] >= 2) return ThreatB;              // XOOO_ * _OOOX (double four)
        if (n[7] >= 1 && n[6] >= 1) return ThreatC; // XOOO_ * _OO (four + open three)
        if (n[7] >= 1 && n[5] >= 1) return ThreatD; // XOOO_ * _OOX
        if (n[7] >= 1 && n[4] >= 1) return ThreatD; // XOOO_ * _O
        if (n[7] >= 1) return ThreatE;              // XOOO_ (blocked four)
        if (n[6] >= 2) return ThreatF;              // OO_ * _OO (double open three / 3x3)
        if (n[6] >= 1 && n[5] >= 1) return ThreatG; // OO_ * _OOX
        if (n[6] >= 1 && n[4] >= 1) return ThreatG; // OO_ * _O
        if (n[6] >= 1) return ThreatH;              // OO_ (open three)

        return ThreatNone;
    }
}

