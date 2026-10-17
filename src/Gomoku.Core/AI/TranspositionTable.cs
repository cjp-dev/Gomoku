using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

public enum BoundType : byte
{
    None = 0,
    Exact = 1,
    LowerBound = 2,
    UpperBound = 3
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TranspositionEntry
{
    public ulong Key;          // 8 bytes
    public short Score;        // 2 bytes
    public byte Depth;         // 1 byte
    public byte BoundAndAge;   // 1 byte (bits 0..1: BoundType, bits 2..7: Age)
    public byte BestX;         // 1 byte (255 if none)
    public byte BestY;         // 1 byte (255 if none)
    public ushort Padding;     // 2 bytes

    public BoundType Bound => (BoundType)(BoundAndAge & 0x03);
    public byte Age => (byte)(BoundAndAge >> 2);

    public Point BestMove => BestX == 255 ? Point.Empty : new Point(BestX, BestY);
}

/// <summary>
/// High-performance 16-byte power-of-two Transposition Table with Zobrist hashing and depth/age replacement.
/// </summary>
public sealed class TranspositionTable
{
    public const int DefaultEntries = 1_048_576; // 2^20 (16 MiB)
    public const int MinEntries = 65_536;        // 2^16 (1 MiB)
    public const int MaxEntries = 16_777_216;    // 2^24 (256 MiB)

    private TranspositionEntry[] _table;
    private ulong _mask;
    private byte _currentAge;

    public long Hits { get; private set; }
    public long Stores { get; private set; }
    public long Collisions { get; private set; }
    public int Capacity => _table.Length;

    public TranspositionTable(int capacity = DefaultEntries)
    {
        int count = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(capacity, MinEntries, MaxEntries));
        _table = new TranspositionEntry[count];
        _mask = (ulong)(count - 1);
    }

    public void NewSearch()
    {
        _currentAge = (byte)((_currentAge + 1) & 0x3F);
        Hits = 0;
        Stores = 0;
        Collisions = 0;
    }

    public void Clear()
    {
        Array.Clear(_table);
        Hits = 0;
        Stores = 0;
        Collisions = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryProbe(ulong key, out TranspositionEntry entry)
    {
        ulong index = key & _mask;
        entry = _table[index];
        if (entry.Key == key && entry.Bound != BoundType.None)
        {
            Hits++;
            return true;
        }

        if (entry.Bound != BoundType.None && entry.Key != key)
        {
            Collisions++;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong key, int depth, int score, BoundType bound, Point bestMove)
    {
        ulong index = key & _mask;
        ref TranspositionEntry slot = ref _table[index];

        bool replace = false;
        if (slot.Bound == BoundType.None || slot.Key == key)
        {
            replace = true;
        }
        else
        {
            // Depth-preferred replacement with aging
            int storedAge = slot.Age;
            int ageDiff = (_currentAge - storedAge) & 0x3F;
            if (ageDiff > 0 || depth >= slot.Depth)
            {
                replace = true;
            }
        }

        if (replace)
        {
            slot.Key = key;
            slot.Score = (short)Math.Clamp(score, -32000, 32000);
            slot.Depth = (byte)Math.Clamp(depth, 0, 255);
            slot.BoundAndAge = (byte)(((byte)bound & 0x03) | (_currentAge << 2));
            slot.BestX = bestMove.IsValid(64, 32) ? (byte)bestMove.X : (byte)255;
            slot.BestY = bestMove.IsValid(64, 32) ? (byte)bestMove.Y : (byte)255;
            Stores++;
        }
    }
}

