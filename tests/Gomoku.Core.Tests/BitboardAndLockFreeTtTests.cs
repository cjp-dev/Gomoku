using Gomoku.Core.AI;
using Gomoku.Core.Engine;
using Gomoku.Core.Models;
using Xunit;

namespace Gomoku.Core.Tests;

public class BitboardAndLockFreeTtTests
{
    [Fact]
    public void Bitboard15_MakeAndUndo_ShouldMaintainConsistency()
    {
        Bitboard15 bb = new();
        Assert.Equal(0, bb.MoveCount);
        Assert.Equal(PieceColor.Black, bb.SideToMove);
        Assert.False(bb.HasWinner);

        bb.MakeMove(7, 7); // Black
        Assert.Equal(1, bb.MoveCount);
        Assert.Equal(PieceColor.White, bb.SideToMove);
        Assert.Equal(PieceColor.Black, bb.GetStone(7, 7));
        Assert.False(bb.IsEmpty(7, 7));

        bb.MakeMove(7, 8); // White
        Assert.Equal(2, bb.MoveCount);
        Assert.Equal(PieceColor.Black, bb.SideToMove);
        Assert.Equal(PieceColor.White, bb.GetStone(7, 8));

        bb.UndoMove();
        Assert.Equal(1, bb.MoveCount);
        Assert.Equal(PieceColor.White, bb.SideToMove);
        Assert.True(bb.IsEmpty(7, 8));

        bb.UndoMove();
        Assert.Equal(0, bb.MoveCount);
        Assert.Equal(PieceColor.Black, bb.SideToMove);
        Assert.True(bb.IsEmpty(7, 7));
        Assert.Equal(0UL, bb.Hash);
    }

    [Fact]
    public void Bitboard15_WinDetection_ShouldDetectHorizontalFive()
    {
        Bitboard15 bb = new();
        // Place 5 stones for Black: (7, 3), (7, 4), (7, 5), (7, 6), (7, 7)
        for (int c = 3; c <= 6; c++)
        {
            bb.MakeMove(7, c); // Black
            bb.MakeMove(8, c); // White
        }
        Assert.False(bb.HasWinner);

        bb.MakeMove(7, 7); // Black's 5th stone
        Assert.True(bb.HasWinner);
    }

    [Fact]
    public void Bitboard15_WinDetection_ShouldDetectDiagonalFive()
    {
        Bitboard15 bb = new();
        // Place 5 diagonal stones for Black: (3, 3), (4, 4), (5, 5), (6, 6), (7, 7)
        for (int i = 3; i <= 6; i++)
        {
            bb.MakeMove(i, i); // Black
            bb.MakeMove(i, i + 1); // White
        }
        Assert.False(bb.HasWinner);

        bb.MakeMove(7, 7); // Black's 5th stone
        Assert.True(bb.HasWinner);
    }

    [Fact]
    public void Bitboard15_ExactFiveAndOverline_ShouldDistinguishAccurately()
    {
        // 4 stones: 0b0000_1111 = 15
        ushort four = 0b0000_1111;
        Assert.False(Bitboard15.HasFiveRun(four));
        Assert.False(Bitboard15.HasExactFive(four));
        Assert.False(Bitboard15.HasOverline(four));

        // Exactly 5 stones: 0b0001_1111 = 31
        ushort five = 0b0001_1111;
        Assert.True(Bitboard15.HasFiveRun(five));
        Assert.True(Bitboard15.HasExactFive(five));
        Assert.False(Bitboard15.HasOverline(five));

        // 6 stones (Overline): 0b0011_1111 = 63
        ushort six = 0b0011_1111;
        Assert.True(Bitboard15.HasFiveRun(six));
        Assert.False(Bitboard15.HasExactFive(six));
        Assert.True(Bitboard15.HasOverline(six));

        // Shifted exact 5: stones at bits 2, 3, 4, 5, 6
        ushort shiftedFive = (ushort)(0b0001_1111 << 2);
        Assert.True(Bitboard15.HasFiveRun(shiftedFive));
        Assert.True(Bitboard15.HasExactFive(shiftedFive));
        Assert.False(Bitboard15.HasOverline(shiftedFive));
    }

    [Fact]
    public void LockFreeTranspositionTable_StoreAndProbe_ShouldPreserveData()
    {
        LockFreeTranspositionTable tt = new(1024);
        ulong key = 0xDEADBEEFCAFEBABEUL;
        Point bestMove = new(7, 7);
        int eval = 1450;
        int depth = 6;
        BoundType bound = BoundType.Exact;

        tt.Store(key, depth, eval, bound, bestMove);

        bool found = tt.TryProbe(key, out int probedDepth, out int probedEval, out BoundType probedBound, out Point probedMove);
        Assert.True(found);
        Assert.Equal(depth, probedDepth);
        Assert.Equal(eval, probedEval);
        Assert.Equal(bound, probedBound);
        Assert.Equal(bestMove, probedMove);

        // Different key should miss
        bool miss = tt.TryProbe(0x123456789ABCDEF0UL, out _, out _, out _, out _);
        Assert.False(miss);
    }

    [Fact]
    public void LockFreeTranspositionTable_ConcurrentStressTest_ShouldBeThreadSafe()
    {
        LockFreeTranspositionTable tt = new(65536);
        const int threadCount = 8;
        const int opsPerThread = 10_000;

        Parallel.For(0, threadCount, threadId =>
        {
            Random rng = new(threadId * 1000 + 42);
            for (int i = 0; i < opsPerThread; i++)
            {
                ulong key = (ulong)rng.Next(1, 20_000);
                int depth = rng.Next(1, 10);
                int eval = rng.Next(-5000, 5000);
                BoundType bound = (BoundType)(rng.Next(1, 4));
                Point move = new(rng.Next(0, 15), rng.Next(0, 15));

                tt.Store(key, depth, eval, bound, move);
                if (tt.TryProbe(key, out int d, out int e, out BoundType b, out Point m))
                {
                    Assert.InRange(d, 1, 10);
                    Assert.InRange(e, -5000, 5000);
                }
            }
        });
    }
}

