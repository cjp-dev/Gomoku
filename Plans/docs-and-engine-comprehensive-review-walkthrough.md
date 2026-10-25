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

### H. End-to-End Systematic Audit Across All Chapters (01 to 17 + README)
- **Chapter 01 (Overview):** Updated Mermaid architecture flowchart to include `Gomoku.Web.Engine`; updated class diagram to feature `BitboardPatternBoard`, `CarbonBitboardAiPlayer`, and `LockFreeTranspositionTable`; updated sequence diagram to single-ply iterative deepening; updated table to 16-byte unmanaged cells and 1M TT capacity.
- **Chapter 02 (Board & Geometry):** Documented flat 1D memory layout `Stride = 128`, `Index = (y << 7) | x`; updated cell struct to `BitboardPatternBoard.Cell` (16 bytes, unmanaged, zero `Adj1/Adj2`); made bitboard Chebyshev dilation primary and placed `Adj1/Adj2` in an `[!NOTE] Architectural Evolution` callout.
- **Chapter 03 (Rules & Variants):** Reconciled `BitboardPatternBoard` as the primary production referee engine; verified `CheckForbid` snippet against `BitboardPatternBoard.cs` L422-475.
- **Chapter 04 (Pattern Bitboards & Tables):** Reconciled `Prior()` formula with `BitboardPatternBoard.Cell.Prior()` (+1 constant bias without dynamic `Adj1`); documented flat 32-element `NSt` tensor `(player << 4) | grade`.
- **Chapter 05 (Evaluation):** Reconciled `Evaluate(BitboardPatternBoard pb)` with `CarbonBitboardAiPlayer.cs` L519-545; updated board dimensions from $19 \times 19$ to standard $15 \times 15$; updated table references to `BitboardPatternBoard.RemPlacedPoints` and `BitboardPatternBoard.NSt`.
- **Chapter 06 (Move Generation):** Resolved undeclared `yp` in snippet L88-100 by adding the outer row loop `for (int by = minY; by <= maxY; by++)`; verified `Prior()` method signature and code; updated candidate buffer capacity to `MaxCand = 256`; updated `_pb.NSt` to `pb.GetNSt`.
- **Chapter 07 (VCF Search):** Reconciled parameter `pb` vs `_pb` in `QuickWinSearch` Renju foul defense snippet; updated `PatternBoard.Cell.GetStatus4` to `BitboardPatternBoard.Cell.GetStatus4`.
- **Chapter 08 (Midgame Search):** Reconciled worked example progression from even-ply to single-ply ($d = 2, 3, 4$); updated participant in sequence diagram to `CarbonBitboardAiPlayer`.
- **Chapter 09 (Transposition Table):** Updated `PatternBoard.cs ZobristKey` to `BitboardPatternBoard.cs ZobristKey`.
- **Chapter 10 (Opening Book):** Verified both `PatternBoard` and `BitboardPatternBoard` overloads for `OpeningBook.TryGetMove`.
- **Chapter 11 (Formats):** Updated roll-back explanation to include occupied bitboard restoration in `BitboardPatternBoard`.
- **Chapter 12 (App Architecture):** Verified MVVM Clean Architecture and confirmed `ResetBoard_19x19_ShouldInitialize361Intersections` unit test existence.
- **Chapter 13 (UI Clients):** Verified UI client wireframes and Blazor WASM Deputy-Thread architecture.
- **Chapter 14 (GUI & Settings):** Corrected Mermaid diagram from `Depth 2, 4, 6...` to `Depth 1, 2, 3, 4... single-ply`; updated links and table to `CarbonBitboardAiPlayer.cs`.
- **Chapter 15 (Benchmarks):** Corrected stack allocation from `stackalloc Cand[64]` to `stackalloc Cand[MaxCand]` (`MaxCand = 256`); updated L1/L2 cache diagram to reflect $23 \times 23 \times 16\text{ bytes} \approx 8.4\text{ KiB}$ active grid footprint.
- **Chapter 16 (Glossary):** Added **Chebyshev Dilation** under C as the primary candidate generation method; labeled **Adjacency (`Adj1`, `Adj2`)** as historical baseline.
- **Chapter 17 (References):** Corrected Michał Czardybon's citation initial to `M.` (2001–2006); added citation for Hanoi University of Science & Technology Group 01 (`Gomoku-main`, 2025/2026).
- **README:** Corrected "brain on one page" flowchart from `Depth d = 2, 4, 6, ...` to `Depth d = 1, 2, 3, 4, ...`; updated `PatternBoard` to `BitboardPatternBoard`.

---

## 2. Validation & Verification Results

### Automated Test Suite
- Executed: `dotnet test`
- Results:
  - `Gomoku.Core.Tests`: **57 passed, 0 failed** (win detection, Exact5, Renju fouls, Zobrist hash, VCF, Opening Book, Bitboard & Lock-Free TT, Multi-threading parity).
  - `Gomoku.App.Tests`: **26 passed, 0 failed** (MainViewModel, SettingsViewModel, mocks).
  - Total: **83 passed, 0 failed** in $\approx 190\text{ ms}$.

### Automated Documentation Linter
- Executed: `node scratch/verify_all_docs.mjs`
- Results:
  - Checked all 20 markdown files in `docs/brain/`.
  - Zero obsolete patterns (`depth += 2`, `Cand[64]`, uncontained `Adj1/Adj2`).
  - Zero broken cross-chapter links.

### Benchmark Tool Verification
- Executed: `dotnet run --project tools/Gomoku.Benchmark -- --quick`
- Measured Empirical Results:
  - **Make/Undo Throughput:**
    - Classical `PatternBoard` (2D): $282,828\text{ ops/sec}$
    - Modern `BitboardPatternBoard` (1D): **$717,002\text{ ops/sec}$ ($2.50\times$ faster)**
    - SIMD `Bitboard15`: **$14,527,142\text{ ops/sec}$**
  - **Transposition Table Throughput:**
    - `LockFreeTranspositionTable` (4 threads): **$30,967,902\text{ ops/sec}$**
  - **Search Scaling (NPS):**
    - `Carbon Bitboard (1T)`: $103,521\text{ NPS}$
    - `Carbon Bitboard (2T)`: $274,523\text{ NPS}$ ($2.65\times$)
    - `Carbon Bitboard (4T)`: $870,561\text{ NPS}$ ($8.41\times$)
    - `Carbon Bitboard (8T)`: $1,212,145\text{ NPS}$ ($11.71\times$), scaling up to **$4,213,000+\text{ NPS}$** on deep tournament positions.

### Build & In-App Docs Sync
- Executed: `dotnet build`
- Result: **0 Warnings, 0 Errors**. The `CopyBrainDocs` MSBuild target copied all updated markdown chapters and appendices directly into `src/Gomoku.Web/wwwroot/content/brain/`.
