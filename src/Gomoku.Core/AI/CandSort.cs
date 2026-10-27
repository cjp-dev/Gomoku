using System.Numerics;
using System.Runtime.CompilerServices;

namespace Gomoku.Core.AI;

/// <summary>
/// Sorts candidates by descending <see cref="CarbonBitboardAiPlayer.Cand.Value"/> with exactly the algorithm of
/// <c>Span&lt;T&gt;.Sort(Comparison&lt;T&gt;)</c> (the .NET introsort in ArraySortHelper, MIT licence), so equal
/// values end up in the same order as before. The comparison is inlined instead of called through a delegate.
/// </summary>
internal static class CandSort
{
    private const int IntrosortSizeThreshold = 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare(in CarbonBitboardAiPlayer.Cand a, in CarbonBitboardAiPlayer.Cand b) => b.Value.CompareTo(a.Value);

    public static void SortDescending(Span<CarbonBitboardAiPlayer.Cand> keys)
    {
        if (keys.Length > 1)
        {
            IntroSort(keys, 2 * (BitOperations.Log2((uint)keys.Length) + 1));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater(Span<CarbonBitboardAiPlayer.Cand> keys, int i, int j)
    {
        if (Compare(keys[i], keys[j]) > 0)
        {
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap(Span<CarbonBitboardAiPlayer.Cand> a, int i, int j) => (a[i], a[j]) = (a[j], a[i]);

    private static void IntroSort(Span<CarbonBitboardAiPlayer.Cand> keys, int depthLimit)
    {
        int partitionSize = keys.Length;
        while (partitionSize > 1)
        {
            if (partitionSize <= IntrosortSizeThreshold)
            {
                if (partitionSize == 2)
                {
                    SwapIfGreater(keys, 0, 1);
                    return;
                }

                if (partitionSize == 3)
                {
                    SwapIfGreater(keys, 0, 1);
                    SwapIfGreater(keys, 0, 2);
                    SwapIfGreater(keys, 1, 2);
                    return;
                }

                InsertionSort(keys[..partitionSize]);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(keys[..partitionSize]);
                return;
            }
            depthLimit--;

            int p = PickPivotAndPartition(keys[..partitionSize]);
            IntroSort(keys[(p + 1)..partitionSize], depthLimit);
            partitionSize = p;
        }
    }

    private static int PickPivotAndPartition(Span<CarbonBitboardAiPlayer.Cand> keys)
    {
        int hi = keys.Length - 1;
        int middle = hi >> 1;

        SwapIfGreater(keys, 0, middle);
        SwapIfGreater(keys, 0, hi);
        SwapIfGreater(keys, middle, hi);

        CarbonBitboardAiPlayer.Cand pivot = keys[middle];
        Swap(keys, middle, hi - 1);
        int left = 0, right = hi - 1;

        while (left < right)
        {
            while (Compare(keys[++left], pivot) < 0) ;
            while (Compare(pivot, keys[--right]) < 0) ;

            if (left >= right)
                break;

            Swap(keys, left, right);
        }

        if (left != hi - 1)
        {
            Swap(keys, left, hi - 1);
        }
        return left;
    }

    private static void HeapSort(Span<CarbonBitboardAiPlayer.Cand> keys)
    {
        int n = keys.Length;
        for (int i = n >> 1; i >= 1; i--)
        {
            DownHeap(keys, i, n);
        }

        for (int i = n; i > 1; i--)
        {
            Swap(keys, 0, i - 1);
            DownHeap(keys, 1, i - 1);
        }
    }

    private static void DownHeap(Span<CarbonBitboardAiPlayer.Cand> keys, int i, int n)
    {
        CarbonBitboardAiPlayer.Cand d = keys[i - 1];
        while (i <= n >> 1)
        {
            int child = 2 * i;
            if (child < n && Compare(keys[child - 1], keys[child]) < 0)
            {
                child++;
            }

            if (!(Compare(d, keys[child - 1]) < 0))
                break;

            keys[i - 1] = keys[child - 1];
            i = child;
        }

        keys[i - 1] = d;
    }

    private static void InsertionSort(Span<CarbonBitboardAiPlayer.Cand> keys)
    {
        for (int i = 0; i < keys.Length - 1; i++)
        {
            CarbonBitboardAiPlayer.Cand t = keys[i + 1];

            int j = i;
            while (j >= 0 && Compare(t, keys[j]) < 0)
            {
                keys[j + 1] = keys[j];
                j--;
            }

            keys[j + 1] = t;
        }
    }
}
