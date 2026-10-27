namespace Gomoku.Core.AI;

/// <summary>
/// Search counters for benchmarks and tuning, collected by worker 0 when
/// <see cref="CarbonBitboardAiPlayer.CollectStats"/> is set. Plain fields, so they cost nothing to share.
/// </summary>
public sealed class SearchStats
{
    public long InteriorNodes;
    public long Leaves;
    public long QuickWinCalls;
    public long QuickWinHits;
    public long TtProbes;
    public long TtHits;
    public long TtCutoffs;
    public long BetaCutoffs;
    public long FirstMoveCutoffs;
    public long CandidateSum;

    public double TtHitRate => TtProbes > 0 ? (double)TtHits / TtProbes : 0;
    public double FirstMoveCutoffRate => BetaCutoffs > 0 ? (double)FirstMoveCutoffs / BetaCutoffs : 0;
    public double AverageCandidates => InteriorNodes > 0 ? (double)CandidateSum / InteriorNodes : 0;

    public void Add(SearchStats other)
    {
        InteriorNodes += other.InteriorNodes;
        Leaves += other.Leaves;
        QuickWinCalls += other.QuickWinCalls;
        QuickWinHits += other.QuickWinHits;
        TtProbes += other.TtProbes;
        TtHits += other.TtHits;
        TtCutoffs += other.TtCutoffs;
        BetaCutoffs += other.BetaCutoffs;
        FirstMoveCutoffs += other.FirstMoveCutoffs;
        CandidateSum += other.CandidateSum;
    }

    public override string ToString() =>
        $"interior {InteriorNodes:N0}, leaves {Leaves:N0}, quick-win {QuickWinHits:N0}/{QuickWinCalls:N0}, " +
        $"TT hits {TtHitRate:P1} (cutoffs {TtCutoffs:N0}), first-move cutoffs {FirstMoveCutoffRate:P1}, " +
        $"candidates {AverageCandidates:F1}";
}
