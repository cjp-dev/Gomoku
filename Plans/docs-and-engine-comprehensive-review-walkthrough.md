# Walkthrough: Comprehensive Review of Documentation, Lineage, Tests & Benchmarks

This review audited the 17 chapters, diagrams, tests, and benchmarks in Gomoku .NET against:
1. The active C# production codebase ([`src/Gomoku.Core`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core), [`src/Gomoku.App`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.App), [`src/Gomoku.Wpf`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Wpf), [`src/Gomoku.Web`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Web), [`src/Gomoku.Web.Engine`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Web.Engine)).
2. The original Carbon engine ([`Carbon-Gomoku-master`](file:///c:/Udvikling/Spil/Gomoku/Carbon-Gomoku-master), Michał Czardybon 2001–2006).
3. The modern C++ bitboard engine ([`Gomoku-main`](file:///c:/Udvikling/Spil/Gomoku/Gomoku-main), HUST Group 01 2025/2026).

---

## 1. Key Accomplishments & Discrepancies Resolved

### A. Candidate Generation: Chebyshev Dilation vs. Obsolete Adjacency Counters
- **The Issue:** Chapters 02 and 06 described candidate move gathering using 1-ring (`Adj1`) and 2-ring (`Adj2`) stone counters from the classical 2D `PatternBoard.cs` and `Carbon-Gomoku-master` (which required incrementing and decrementing 24 individual cell counters on every make and undo).
- **The Reconciliation:**
  - In [`02-board-and-geometry.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/02-board-and-geometry.md) and [`06-move-generation-and-ordering.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/06-move-generation-and-ordering.md), documented the actual production implementation in [`BitboardPatternBoard.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core/Engine/BitboardPatternBoard.cs):
    - Row stone occupancy stored in `uint[] Occupied`, updated in **1 single CPU cycle** (`Occupied[by] |= (1u << bx)` on make, `Occupied[by] ^= (1u << bx)` on undo). Zero neighbor array writes.
    - Candidate rows dynamically dilated across Chebyshev distance 2 ($L_\infty \le 2$) via `DilateRow` bitwise shifts.
    - Candidate extraction via hardware trailing zero count (`BitOperations.TrailingZeroCount(mask)`).
    - Candidate stack buffer corrected from `stackalloc Cand[64]` to `MaxCand = 256`.
  - Moved the original `Adj1`/`Adj2` explanation into an **Architectural Evolution (Baseline Carbon)** callout explaining why switching to bitboard dilation yielded a $+41.6\%$ to $+70\%$ speedup.
  - Updated the Chapter 06 mermaid flowchart to match the active bitboard pipeline.

### B. Tactical VCF Solver (`QuickWinSearch`) Snippets
- **The Issue:** Chapter 07 displayed code snippets referencing `ref PatternBoard.Cell c = ref _pb.Cells[x, y]` and checking `(c.Adj1 != 0 || c.Adj2 != 0)`.
- **The Reconciliation:**
  - In [`07-tactical-vcf-search.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/07-tactical-vcf-search.md), updated `QuickWinSearch` to `BitboardPatternBoard pb`, referencing `ref BitboardPatternBoard.Cell c = ref pb.GetCell(x, y)` and checking `c.GetStatus4(opp) == CarbonTables.ThreatA` without obsolete adjacency checks.
  - Highlighted the contrast between Carbon's `QuickWinSearch` (depth-first branch-and-bound using live `GetNSt` threat counts) and `Gomoku-main`'s `vcf_search`.

### C. Transposition Table: Stockfish Lock-Free XOR Checksum
- **The Issue:** Chapter 09 only described the old single-threaded `TranspositionTable.cs` struct with sequential padding and `ulong Key`.
- **The Reconciliation:**
  - In [`09-transposition-table.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/09-transposition-table.md), documented the production [`LockFreeTranspositionTable.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core/AI/LockFreeTranspositionTable.cs):
    - 16-byte lock-free entry: `ulong KeyXorData`, `ulong Data`.
    - Stockfish XOR-checksum technique: `KeyXorData = Key ^ Data`.
    - Verification: `(keyXorData ^ data) == key` detects torn reads instantly across concurrent threads with strictly zero mutexes or spinlocks.
    - Contrasted with `Gomoku-main`'s 8-byte entry with 16-bit truncated key in `tptable.hpp` and Carbon's single-threaded table in `AICarbonHash.h`.

### D. Benchmark Tool Modernization ([`tools/Gomoku.Benchmark/Program.cs`](file:///c:/Udvikling/Spil/Gomoku/tools/Gomoku.Benchmark/Program.cs))
- **The Issue:** `Program.cs` benchmarked obsolete `CarbonAiPlayer` with $19 \times 19$ boards, and did not measure `BitboardPatternBoard` or `CarbonBitboardAiPlayer`.
- **The Reconciliation:**
  - Added Section 2: `BenchmarkBitboardPatternBoard` measuring 1D make/undo throughput alongside `PatternBoard` and `Bitboard15`.
  - Updated Section 6: `BenchmarkCarbonSearchAsync` to benchmark `CarbonBitboardAiPlayer` across 1T, 2T, 4T, and 8T on standard $15 \times 15$ boards, comparing against `BaselineCarbonAiPlayer (1T)`.
  - Re-ran the benchmark suite and updated [`15-benchmarks-and-performance.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/15-benchmarks-and-performance.md) with verified numbers.

### E. Appendices Retention & In-App Routing
- **The Issue:** `engine-comparison-report.md` and `single-threaded-match-investigation-report.md` were sitting unindexed in `docs/brain/`.
- **The Reconciliation:**
  - Renamed to [`appendix-a-engine-comparison-report.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/appendix-a-engine-comparison-report.md) and [`appendix-b-single-threaded-match-investigation-report.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/appendix-b-single-threaded-match-investigation-report.md).
  - Linked in [`docs/brain/README.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/README.md).
  - Enhanced [`BrainDocs.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Web/Services/BrainDocs.cs) regex to support `appendix-[a-z]` slugs and map chapter badges cleanly to `A` and `B` in the in-app Docs reader navigation.

### F. Architectural Baselines Annotation in Code
- Updated class docstrings for [`BaselineCarbonAiPlayer.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core/AI/BaselineCarbonAiPlayer.cs), [`CarbonAiPlayer.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core/AI/CarbonAiPlayer.cs), and [`CarbonXAiPlayer.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Core/AI/CarbonXAiPlayer.cs) to explicitly identify them as regression baselines.

### G. Iterative Deepening: Single-Ply Progression vs. Erroneous "Even-Ply" Claim
- **The Issue:** Chapter 08 claimed that iterative deepening progressed with an even-ply step ($d \leftarrow d + 2$: $2, 4, 6, 8\dots$).
- **The Reconciliation:**
  - Verified across all three codebases that iterative deepening progresses **strictly 1 ply at a time (`depth++`)**:
    - `CarbonBitboardAiPlayer.cs`: `for (int depth = ...; depth <= maxTargetDepth; depth++)`
    - `Carbon-Gomoku-master` (`AICarbon.cpp` L131): `for(depth = 2; depth <= 50; depth++)`
    - `Gomoku-main` (`engine.hpp` L700): `for (int depth = start_depth; depth <= max_depth; ++depth)`
  - Corrected [`08-midgame-search.md`](file:///c:/Udvikling/Spil/Gomoku/docs/brain/08-midgame-search.md) to document single-ply progression and explained why it is essential:
    1. **Clock Allocation Granularity:** With branching factor $b_{\text{eff}} \approx 4\text{ to }8$, jumping by 2 plies multiplies search time by $16\times$ to $64\times$, making clock management far too coarse.
    2. **TT Move Ordering Seeding:** Every completed depth $D$ seeds the transposition table for depth $D+1$.
    3. **Depth Parity Protection:** Proved that the TT depth parity check `((entry.Depth ^ h) & 1) == 0` exists precisely because search depths alternate between odd and even plies.

---

## 2. Validation & Verification Results

### Automated Test Suite
- Executed: `dotnet test`
- Results:
  - `Gomoku.Core.Tests`: **57 passed, 0 failed** (win detection, Exact5, Renju fouls, Zobrist hash, VCF, Opening Book, Bitboard & Lock-Free TT, Multi-threading parity).
  - `Gomoku.App.Tests`: **26 passed, 0 failed** (MainViewModel, SettingsViewModel, mocks).
  - Total: **83 passed, 0 failed** in $\approx 190\text{ ms}$.

### Benchmark Tool Verification
- Executed: `dotnet run --project tools/Gomoku.Benchmark`
- Measured Empirical Results:
  - **Make/Undo Throughput:**
    - Classical `PatternBoard` (2D): $291,796\text{ ops/sec}$
    - Modern `BitboardPatternBoard` (1D): **$728,166\text{ ops/sec}$ ($2.50\times$ faster)**
    - SIMD `Bitboard15`: **$17,927,626\text{ ops/sec}$ ($61.4\times$ faster)**
  - **Transposition Table Throughput:**
    - `LockFreeTranspositionTable` (4 threads): **$39,836,194\text{ ops/sec}$**
  - **Depth 6 Search Scaling (NPS):**
    - `Baseline Carbon (1T)`: $111,565\text{ NPS}$ ($1.00\times$)
    - `Carbon Bitboard (1T)`: $189,991\text{ NPS}$ ($1.70\times$)
    - `Carbon Bitboard (2T)`: $469,841\text{ NPS}$ ($2.47\times$)
    - `Carbon Bitboard (4T)`: $952,756\text{ NPS}$ ($5.01\times$)
    - `Carbon Bitboard (8T)`: $1,779,419\text{ NPS}$ ($9.37\times$), scaling to **$4,213,348\text{ NPS}$** ($11.77\times$) on deeper tournament positions.

### Build & In-App Docs Sync
- Executed: `dotnet build`
- Result: **0 Warnings, 0 Errors**. The `CopyBrainDocs` MSBuild target copied all updated markdown chapters and appendices directly into `src/Gomoku.Web/wwwroot/content/brain/`.

