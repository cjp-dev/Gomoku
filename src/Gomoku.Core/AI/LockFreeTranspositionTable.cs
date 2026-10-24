using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Gomoku.Core.Models;

namespace Gomoku.Core.AI;

/// <summary>
/// High-performance 16-byte lock-free Transposition Table supporting concurrent
/// multi-threaded access (Lazy SMP) and zero-overhead single-threaded search.
/// Utilizes the Stockfish XOR-checksum technique (Word0 = Key ^ Word1) to prevent
/// torn reads without requiring locks or mutexes.
/// </summary>
public sealed class LockFreeTranspositionTable
{
    public const int DefaultCapacity = 1_048_576; // 1M entries = 16 MB

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct Entry
    {
        [FieldOffset(0)] public ulong KeyXorData;
        [FieldOffset(8)] public ulong Data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PackData(Point bestMove, sbyte depth, BoundType bound, int eval, byte age)
        {
            // Packed 64-bit data word:
            // Bits 0..7: BestMove.X (byte)
            // Bits 8..15: BestMove.Y (byte)
            // Bits 16..23: Depth (sbyte)
            // Bits 24..31: Bound (byte)
            // Bits 32..55: Eval (24-bit signed int)
            // Bits 56..63: Age / Generation (byte)
            uint evalSigned = (uint)(eval & 0x00FF_FFFF);
            ulong d = (ulong)(byte)bestMove.X |
                      ((ulong)(byte)bestMove.Y << 8) |
                      ((ulong)(byte)(depth + 128) << 16) |
                      ((ulong)(byte)bound << 24) |
                      ((ulong)evalSigned << 32) |
                      ((ulong)age << 56);
            return d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackData(ulong data, out Point bestMove, out int depth, out BoundType bound, out int eval, out byte age)
        {
            int x = (int)(byte)(data & 0xFF);
            int y = (int)(byte)((data >> 8) & 0xFF);
            bestMove = (x == 255 || y == 255) ? Point.Empty : new Point(x, y);
            depth = (int)(byte)((data >> 16) & 0xFF) - 128;
            bound = (BoundType)(byte)((data >> 24) & 0xFF);

            // Sign-extend 24-bit eval to 32-bit int
            int rawEval = (int)((data >> 32) & 0x00FF_FFFF);
            if ((rawEval & 0x0080_0000) != 0)
            {
                rawEval |= unchecked((int)0xFF00_0000);
            }
            eval = rawEval;

            age = (byte)((data >> 56) & 0xFF);
        }
    }

    private readonly Entry[] _table;
    private readonly ulong _mask;
    private byte _currentAge;
    private long _hits;
    private long _stores;

    public int Capacity { get; }
    public long Hits => Volatile.Read(ref _hits);
    public long Stores => Volatile.Read(ref _stores);

    public LockFreeTranspositionTable(int capacity = DefaultCapacity)
    {
        // Round up to power of 2 for fast bitwise indexing
        Capacity = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1024, capacity));
        _mask = (ulong)(Capacity - 1);
        _table = new Entry[Capacity];
        _currentAge = 0;
    }

    public void NewSearch()
    {
        _currentAge = (byte)((_currentAge + 1) & 0x7F);
        Volatile.Write(ref _hits, 0);
        Volatile.Write(ref _stores, 0);
    }

    public void Clear()
    {
        Array.Clear(_table);
        _currentAge = 0;
        Volatile.Write(ref _hits, 0);
        Volatile.Write(ref _stores, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryProbe(ulong key, out int depth, out int eval, out BoundType bound, out Point bestMove)
    {
        ulong index = key & _mask;
        ref Entry entry = ref _table[index];

        ulong keyXorData = Volatile.Read(ref entry.KeyXorData);
        ulong data = Volatile.Read(ref entry.Data);

        // Verify lock-free XOR checksum against probe key
        if ((keyXorData ^ data) == key)
        {
            Entry.UnpackData(data, out bestMove, out depth, out bound, out eval, out _);
            Interlocked.Increment(ref _hits);
            return true;
        }

        bestMove = Point.Empty;
        depth = 0;
        eval = 0;
        bound = BoundType.None;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Store(ulong key, int depth, int eval, BoundType bound, Point bestMove)
    {
        ulong index = key & _mask;
        ref Entry entry = ref _table[index];

        ulong oldData = Volatile.Read(ref entry.Data);
        ulong oldKey = Volatile.Read(ref entry.KeyXorData) ^ oldData;

        // Replacement scheme:
        // Protect deeper entries from being overwritten by shallower searches
        if (oldData != 0)
        {
            Entry.UnpackData(oldData, out Point oldMove, out int oldDepth, out BoundType oldBound, out _, out byte oldAge);
            int ageDiff = (_currentAge - oldAge) & 0x7F;

            if (ageDiff == 0)
            {
                // In current search, never let a shallower search overwrite a deeper entry
                if (depth < oldDepth && bound != BoundType.Exact)
                {
                    return;
                }

                // If collision with different key, preserve existing entry if it is deeper
                if (oldKey != key && depth <= oldDepth)
                {
                    return;
                }
            }

            // Preserve best move hint if new search didn't provide one
            if (bestMove == Point.Empty && oldKey == key && oldMove != Point.Empty)
            {
                bestMove = oldMove;
            }
        }

        Point movePoint = (bestMove == Point.Empty || bestMove.X < 0 || bestMove.Y < 0) ? new Point(255, 255) : bestMove;
        ulong newData = Entry.PackData(movePoint, (sbyte)Math.Clamp(depth, -128, 127), bound, eval, _currentAge);
        ulong newKeyXorData = key ^ newData;

        Volatile.Write(ref entry.Data, newData);
        Volatile.Write(ref entry.KeyXorData, newKeyXorData);
        Interlocked.Increment(ref _stores);
    }
}
