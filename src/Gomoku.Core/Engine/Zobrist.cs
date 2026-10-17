using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// Deterministic 64-bit Zobrist hashing for Gomoku board states.
/// </summary>
public static class Zobrist
{
    // [color 0..1, x 0..63, y 0..31]
    private static readonly ulong[,,] PieceKeys = new ulong[2, 64, 32];
    public static readonly ulong SideToMoveKey;

    static Zobrist()
    {
        // Deterministic PRNG seeded with "GOMO" (0x474F4D4F)
        Random rng = new(0x474F4D4F);

        Span<byte> buf = stackalloc byte[8];
        for (int c = 0; c < 2; c++)
        {
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    rng.NextBytes(buf);
                    PieceKeys[c, x, y] = BitConverter.ToUInt64(buf);
                }
            }
        }

        rng.NextBytes(buf);
        SideToMoveKey = BitConverter.ToUInt64(buf);
    }

    public static ulong GetKey(PieceColor color, int x, int y)
    {
        if (color is PieceColor.Black or PieceColor.White && x >= 0 && x < 64 && y >= 0 && y < 32)
        {
            return PieceKeys[(int)color, x, y];
        }
        return 0UL;
    }

    public static ulong ComputeHash(BoardState state)
    {
        ulong hash = 0UL;
        for (int y = 0; y < state.Size.Height; y++)
        {
            for (int x = 0; x < state.Size.Width; x++)
            {
                PieceColor color = state.GetCell(x, y);
                if (color is PieceColor.Black or PieceColor.White)
                {
                    hash ^= PieceKeys[(int)color, x, y];
                }
            }
        }

        if (state.SideToMove == PieceColor.White)
        {
            hash ^= SideToMoveKey;
        }

        return hash;
    }
}

