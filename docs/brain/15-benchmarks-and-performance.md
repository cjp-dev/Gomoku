# 15 – Benchmarks and performance

[Back to the index](README.md)

## In short

Gomoku engines require extreme computational throughput: evaluating deep tactical combinations and searching 10 to 14 plies ahead within strict tournament time budgets demands hundreds of thousands of node evaluations per second.

Gomoku .NET achieves performance parity with native C++ engines through careful systems-level engineering:
- **Zero Heap Allocations on Hot Paths:** Candidate generation uses `stackalloc Cand[MaxCand]` (`MaxCand = 256`), bitboards operate on primitives, and move undo/redo operates via pre-allocated structures.
- **Incremental Pattern Bitboard Maintenance:** `MakeMove()` updates 4-directional ray bitmasks, threat tensors and the stone score in strictly $\mathcal{O}(1)$ time; `UndoMove()` copies the 33 changed cells back from an undo journal, and `Evaluate()` is a single subtraction.
- **Empirical Throughput:**
  - `PatternBoard` make/undo throughput: **$1,340,000+\text{ ops/sec}$**.
  - Transposition Table probe/store throughput: **$167,000,000+\text{ ops/sec}$**.
  - Single-threaded search throughput: **$1.1\text{--}1.3\text{M NPS}$** on the benchmark suite (Linux VM, i7-12850HX; $\approx 0.8\text{M}$ before the 2026 optimisations, $538,000+$ in older desktop measurements).
  - Browser (WASM AOT, 4 threads): **$4.0\text{M NPS}$**; first reply after H8 to depth 12 in $3.2\text{ s}$ (was $29.0\text{ s}$).
  - Multi-threaded Lazy SMP throughput: **$4,213,000+\text{ NPS}$** ($11.77\times$ the baseline NPS on 8 cores, older measurement); time to depth scales much less (see below).

---

## Benchmark suite (`Gomoku.Benchmark`)

The solution includes a dedicated CLI profiling suite located in `tools/Gomoku.Benchmark`:

```powershell
# Run full performance benchmark suite in Release mode:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release

# Run fast regression check:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --quick

# Run search efficiency and branching factor reduction benchmark:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --efficiency [--depth 10]

# Fixed-depth position suite (38 positions, 1 thread, fresh table, median of N runs):
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --suite [--runs 5] [--threads 1] [--options "lmr=0"] [--depth-offset 0] [--json run.json] [--stats]

# Compare two suite runs position by position:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --compare a.json b.json

# Paired A/B match between two option sets in the same build:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --ab --a "lmr=1" --b "lmr=0" --games 400 --time 0.2 [--time-b 0.2] --threads 1 [--parallel 8] [--variant Freestyle]

# Check that the 64 recorded forced wins are still found (optionally with other options):
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --tactical [--options "pvs=1"]

# Regenerate the suite, the signature and the forced wins / only the signature:
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --gen-suite [--target 1.0] [--signature 0.03]
dotnet run --project tools/Gomoku.Benchmark/Gomoku.Benchmark.csproj -c Release -- --update-signature
```

---

## Engine test harness: suite, signature and A/B matches

The 2026 optimisation series was measured with a harness that separates two questions: *is a change only faster* (it must search exactly the same tree), and *does a change that alters the search play better*.

```mermaid
flowchart LR
    Gen["--gen-suite<br/>self-play in Freestyle, Exact5, Renju"] --> Suite["suite-positions.txt<br/>38 positions, depth per position"]
    Gen --> Sig["engine-signature.txt<br/>best move, score, nodes"]
    Gen --> Tac["tactical-positions.txt<br/>64 forced wins"]
    Suite --> Run["--suite / --compare<br/>nodes, time, NPS"]
    Sig --> Tests["EngineSignatureTests<br/>(xUnit)"]
    Tac --> Tests
    Tac --> TacCli["--tactical --options X"]
    Opts["CarbonSearchOptions<br/>name=1,other=0"] --> Run
    Opts --> AB["--ab --a X --b Y<br/>paired match, 95% CI, Elo"]
```

- **`CarbonSearchOptions`** ([CarbonSearchOptions.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonSearchOptions.cs)): switches for search features (`Pvs`, `Lmr`, `LmrFromMove`, `LmrMinDepth`, `LmrDeepFromMove`, `CarbonCandidates`), so a feature can be compared on and off in one build. `Parse` reads strings such as `"lmr=0,carbonCandidates=1"` (property names, case-insensitive; unknown names throw); `Default` is what the game plays with.
- **`CollectStats` / `SearchStats`** ([SearchStats.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/SearchStats.cs)): when `CarbonBitboardAiPlayer.CollectStats` is set, worker 0 counts interior nodes, leaves, quick-win calls and hits, TT probes, hits and cutoffs, beta cutoffs and first-move cutoffs, and the candidate count per node (`--suite --stats` prints them). Off by default.
- **`--gen-suite`** plays deterministic self-play games (fixed depth 4, no book) from canonical openings in all three variants, samples positions at plies 8, 14, 20 and 26 (skipping positions that are already decided), and gives each the deepest depth that finishes within the target time (default $1.0\text{ s}$). It writes [suite-positions.txt](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/suite-positions.txt) (38 positions), the signature at a shallower depth (target $0.03\text{ s}$) to [engine-signature.txt](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/TestData/engine-signature.txt), and the forced wins (winner to move 2 to 5 own moves before the end, at the smallest depth up to 10 that reports the win) to [tactical-positions.txt](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/TestData/tactical-positions.txt) (64 positions).
- **`--suite`** first runs a shallow warm-up pass, then searches every position `--runs` times (default 5) on a fresh, pre-cleared table, reports the median time and fails if a 1-thread search is not deterministic. `--depth-offset` shifts all depths, `--only` filters by name, `--json` saves the run for **`--compare`**, which prints the node and time ratios per position and whether the result (nodes, best move, score, depth) is identical.
- **`--ab`** plays a paired match between two option sets: openings are the 26 canonical three-stone openings, each extended by a fourth stone next to the first three; every opening is played twice with colours swapped, games run in parallel (`--time-b` can give B a different time per move), and the result is reported as a score with a 95% confidence interval and the Elo difference with its interval.
- **[EngineSignatureTests](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/EngineSignatureTests.cs)** checks, at a fixed depth with 1 thread, a fresh table and no book, that each of the 38 signature positions gives the recorded best move, score and node count, and that each of the 64 forced wins is still found. A pure speed-up keeps all three; a deliberate change in the search updates the signature with `--update-signature`.
- `GOMOKU_REPO` points a copied build of the benchmark at the repository (to find the suite and test data).

---

## Engine optimisation results (2026)

Measured on a Linux VM (i7-12850HX), suite of 38 positions at fixed depth, 1 thread. "Suite time" is the stored run in [tools/Gomoku.Benchmark/results](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/results); "change" is the commit's interleaved comparison against the previous step. Steps that are marked "same search" kept the signature (38/38) unchanged.

| Step | Change | Suite nodes | Suite time | NPS | Change | Match (paired, $0.2\text{ s}$/move) |
|---|---|--:|--:|--:|--:|---|
| Baseline | – | 14,882,266 | 18.7 s | 0.79M | – | – |
| A5 | `CandSort`: introsort with the comparison inlined | 14,882,266 | 18.6 s | 0.80M | $-3.0\%$ time | same search |
| A4a | Undo journal (33 cells + `NSt` + `StoneScore`) | 14,882,266 | 14.9 s | 1.00M | $-19.5\%$ time | same search |
| A2 | Incremental evaluation (`StoneScore`, $\mathcal{O}(1)$ `Evaluate`) | 14,882,266 | 13.5 s | 1.10M | $-7.4\%$ time | same search |
| A4b | Lean make (unrolled directions, fewer `NSt` writes) | 14,882,266 | 11.4 s | 1.31M | $-15.9\%$ time | same search |
| A1 | No shared TT hit/store counters | 14,882,266 | 11.3 s | 1.32M | within noise | same search |
| B6 | Carbon's own candidate set (`CarbonCandidates`) | 12,751,482 | 10.4 s | 1.23M | $-14.3\%$ nodes, $-10.3\%$ time | $+33$ Elo $[+9, +59]$ (1T, 730 games); $+23$ $[-16, +63]$ (8T, 300) |
| B4 | Late move reductions (`Lmr`) | 6,493,648 | 5.7 s | 1.14M | $-49.1\%$ nodes, $-45.3\%$ time | $+48$ Elo $[+24, +74]$ (1T, 730 games); $+30$ $[-9, +70]$ (8T, 300); $+12$ at $1\text{ s}$/move |

In total the suite went from about $18.9\text{ s}$ (stored baseline runs: $18.7$ and $19.4\text{ s}$) to $5.7\text{ s}$ and from $14.9\text{M}$ to $6.5\text{M}$ nodes, while single-thread NPS rose from about $0.8\text{M}$ to $1.1\text{--}1.3\text{M}$ (it drops a little with B6 and B4, which change the tree).

**Browser** (Blazor WebAssembly AOT, threaded engine, first reply after H8, fixed depth 12):

| Threads | Before | After |
|---|--:|--:|
| 1 | $55.1\text{ s}$ ($536,000\text{ NPS}$) | $4.3\text{ s}$ ($1,150,000\text{ NPS}$) |
| 4 | $29.0\text{ s}$ ($1,890,000\text{ NPS}$) | $3.2\text{ s}$ ($4,000,000\text{ NPS}$) |

**Why the browser gained $12.8\times$ and the suite only $3.3\times$.** The difference comes mostly from the position, not from WebAssembly: the same position (reply to H8, Freestyle, depth 12, 1 thread) on the desktop build went from $30.3\text{ s}$ to $2.73\text{ s}$, which is $11.1\times$. The speed-up is the product of fewer nodes and more nodes per second:

| | Fewer nodes | Higher NPS | Total |
|---|--:|--:|--:|
| Suite, desktop (38 positions, depths 5–10) | $14.9\text{M} \to 6.5\text{M}$ = $2.3\times$ | $764\text{k} \to 1.11\text{M}$ = $1.45\times$ | $3.3\times$ |
| Reply to H8, depth 12, desktop | $29.6\text{M} \to 5.0\text{M}$ = $5.9\times$ | $975\text{k} \to 1.83\text{M}$ = $1.88\times$ | $11.1\times$ |
| Reply to H8, depth 12, browser | $29.6\text{M} \to 5.0\text{M}$ = $5.9\times$ | $536\text{k} \to 1.15\text{M}$ = $2.15\times$ | $12.8\times$ |

The browser and the desktop count exactly the same nodes.

- **More nodes saved ($5.9\times$ against $2.3\times$).** Both search changes work best in a quiet, deep position.
  - Late move reductions only shorten quiet moves. With one stone on the board almost every move is quiet, whereas the suite's mid-game positions (8–26 stones) are full of threats, which are never shortened.
  - The saving of late move reductions grows with depth. A reduced move cuts 2 plies off its whole subtree, and this repeats at every level.
  - Carbon's candidate set removes the most moves on an almost empty board, where the old $5 \times 5$ squares around the stones do not yet overlap.
- **More gained per node ($1.88\times$ against $1.45\times$).** This was not profiled. A likely cause is that late move reductions move more of the work into cheap searches close to the leaves.
- **WebAssembly adds only about 14%** ($2.15\times$ against $1.88\times$ per node). A plausible reason, not measured, is that the speed-ups removed work that costs more under WASM AOT, such as calls through a delegate in the sort.

So $3.3\times$ is the typical gain over many kinds of positions, and $12.8\times$ is close to the best case: a quiet opening searched deep.

**Lazy SMP scales poorly in time to depth.** NPS grows with the threads, but on the suite 4 and 8 threads reach the same depths only $\approx 1.6\times$ faster than 1 thread (and $4.3\text{ s} \to 3.2\text{ s}$ in the browser). The extra nodes mostly duplicate work.

**Tried and rejected:**

| Experiment | Result |
|---|---|
| Principal Variation Search (`Pvs`) | $-3.4\%$ nodes, match $50.0\%$, $0$ Elo $[-25, +25]$; with LMR only $-17\%$ nodes against $-49\%$ for LMR alone. Kept as an option, off. |
| History heuristic as a tie-break | $-13.9\%$ nodes but only $-6.4\%$ time; matches $+16$ $[-9, +41]$ (1T) and $+7$ $[-32, +46]$ (8T). |
| Shared depth for the Lazy SMP helpers | No gain in time to depth; 8-thread match $46.7\%$, $-23$ Elo $[-63, +16]$. |
| `LmrMinDepth = 2` | $-14.8\%$ nodes; match $50.8\% \pm 3.6\%$, $+5$ Elo $[-20, +30]$. |
| `LmrFromMove = 2`, `LmrDeepFromMove = 8` | Under $3\%$ fewer nodes. |
| 4-way bucket transposition table | Neutral in matches; the table stays direct-mapped. |

---

## Empirical benchmark results

The following older measurements were captured on a modern 64-bit multi-core machine running .NET 10 (Release configuration), before the 2026 optimisation series above:

### 1. Board Representation Throughput (Make/Undo Speed)

Empirical make/undo measurements captured via `dotnet run --project tools/Gomoku.Benchmark` on $15 \times 15$ boards:

| Board Implementation | Total Ops Tested | Elapsed Time | Throughput (ops/sec) | Speedup vs Baseline |
|---|:---:|:---:|:---:|:---:|
| **Classical `PatternBoard` (2D array + 24 neighbor writes)** | $600,000$ | $2,056\text{ ms}$ | $291,796\text{ ops/sec}$ | $1.00\times$ |
| **`BitboardPatternBoard` (1D Stride-128 + bitboard candidates, before the journal undo)** | $600,000$ | $823\text{ ms}$ | **$728,166\text{ ops/sec}$** | **$2.50\times$ faster** |
| **`Bitboard15` (16-bit SIMD registers)** | $600,000$ | $33\text{ ms}$ | **$17,927,626\text{ ops/sec}$** | **$61.4\times$ faster** |

*Note: All implementations maintain 100% board consistency across arbitrary make/undo sequences.*

### 2. Classical `PatternBoard` Make/Undo Details
Measures raw throughput of stone placement, 4-ray bitmask updates, `STATUS1` and `STATUS4` tensor updates, `NSt` live threat counter adjustments, and incremental Zobrist hashing:

$$\text{Test: } 100,000\text{ consecutive 3-ply make/undo cycles } (600,000\text{ operations}) \text{ on } 15 \times 15\text{ board}$$

| Metric | Measured Value | Unit |
|---|:---:|:---:|
| **Total Make/Undo Operations** | $600,000$ | ops |
| **Elapsed Time** | $447$ | ms |
| **Throughput** | **$1,340,281$** | **ops / sec** |
| **Average Latency per Op** | **$746$** | **nanoseconds** |
| **Consistency Verification** | **PASS** (100% bitmask match) | - |

Each full move make-and-undo cycle (modifying 32 ray bitmasks and updating 8 neighboring cells across 4 directions) executes in under **$0.75\text{ microseconds}$**.

---

### 2. Transposition Table Masking & Cache Speed
Measures raw storage and probe throughput across power-of-two table sizes ($N = 2^k$):

$$\text{Test: } 1,000,000\text{ store-and-probe cycles } (2,000,000\text{ operations}) \text{ per capacity with multiplicative 64-bit keys}$$

| Entries ($N$) | RAM Size | Elapsed Time | Throughput | Hash Hits |
|:---:|:---:|:---:|:---:|:---:|
| **$65,536$** ($2^{16}$) | $1\text{ MiB}$ | $11.8\text{ ms}$ | **$169,093,153\text{ ops/sec}$** | $1,000,000$ |
| **$262,144$** ($2^{18}$) | $4\text{ MiB}$ | $5.8\text{ ms}$ | **$341,629,230\text{ ops/sec}$** | $1,000,000$ |
| **$1,048,576$** ($2^{20}$) | $16\text{ MiB}$ | $11.9\text{ ms}$ | **$167,965,601\text{ ops/sec}$** | $1,000,000$ |
| **$4,194,304$** ($2^{22}$) | $64\text{ MiB}$ | $31.9\text{ ms}$ | **$62,623,682\text{ ops/sec}$** | $1,000,000$ |

```mermaid
xychart-beta
    title "Transposition Table Throughput vs Table Size"
    x-axis ["65k (1MB)", "262k (4MB)", "1M (16MB)", "4M (64MB)"]
    y-axis "Million Ops / Sec" 0 --> 400
    bar [169, 341, 168, 63]
```

*These figures are for the reference `TranspositionTable` (which counts hits and stores); the lock-free table has no counters.* At $4\text{ MiB}$ ($262,144$ entries), the entire table fits inside the CPU L3 cache, delivering peak throughput of **$341\text{ million}$ operations per second** ($2.9\text{ ns}$ per probe). At $64\text{ MiB}$, main RAM memory bus access limits throughput to $62.6\text{ M ops/sec}$.

---

### 3. Minimax Search Throughput & Depth Scaling
Measures iterative deepening search speed on standard opening positions (Black H8, White H9, Black I8) without opening book assistance:

| Board Size | Target Depth | Best Move | Nodes Searched | Elapsed Time | Search Speed (NPS) |
|:---:|:---:|:---:|:---:|:---:|:---:|
| **$15 \times 15$** | Depth 4 | **G8** | $924$ | $14\text{ ms}$ | $64,717\text{ NPS}$ |
| **$19 \times 19$** | Depth 4 | **I10** | $924$ | $7\text{ ms}$ | $117,274\text{ NPS}$ |
| **$15 \times 15$** | Depth 6 | **J8** | $10,285$ | $34\text{ ms}$ | **$294,915\text{ NPS}$** |
| **$19 \times 19$** | Depth 6 | **L10** | $11,055$ | $29\text{ ms}$ | **$380,690\text{ NPS}$** |

Notice that depth 6 searches expand only $10,000\text{ to }11,000$ nodes in under $35\text{ milliseconds}$. Thanks to `Prior()` move ordering and TT cutoffs, the effective branching factor is:
$$b_{\text{eff}} = \sqrt[6]{11,055} \approx 4.72$$

An effective branching factor of $4.72$ (out of 361 legal squares) demonstrates that the engine prunes over $98.7\%$ of the theoretical game tree.

---

### 4. 100-Game Head-to-Head Tournament: Carbon AI vs. Carbon-X

To investigate whether a pure sliding bitboard approach could outperform Carbon's directional ray bitboards and precomputed threat tables, a 100-game head-to-head tournament was executed under tournament conditions:
- **Time control:** Fixed $0.20\text{ s}$ per move.
- **Positions:** 25 standard opening positions played in alternating color pairs.

| Match | Engines | Wins | Draws | Win Rate | Avg Depth | Avg NPS |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **Match 1 (1T vs 1T)** | **Carbon AI (1T)** vs **Carbon-X (1T)** | **33 - 17** | 0 | **66.0% vs 34.0%** | 4.5 vs 5.5 | 344,702 vs 169,165 |
| **Match 2 (8T vs 1T)** | **Carbon AI (1T)** vs **Carbon-X (8T)** | **34 - 16** | 0 | **68.0% vs 32.0%** | 4.5 vs 5.6 | 346,574 vs 1,237,851 |
| **Overall Aggregate** | **Carbon AI Total** | **67 - 33** | 0 | **67.0%** | - | - |

**Key Findings:** While Carbon-X achieved fast primitive bitboard evaluations (7.6M ops/sec) and searched 1 ply deeper, Carbon AI won overwhelmingly (**67% to 33%**) because:
1. Carbon Gomoku's fine-grained `Rank[Config]` directional heuristics encapsulate decades of Gomocup tournament refinements that sliding bitboards lacked.
2. Carbon's `QuickWinSearch` VCF solver detects forced checkmates up to 24 plies deep that standard depth-5 minimax cannot see.
Full findings are documented in [`appendix-a-engine-comparison-report.md`](appendix-a-engine-comparison-report.md).

---

### 5. Multi-Threaded Lazy SMP Acceleration & Final Tournament

Following the investigation, the primary Carbon AI engine was accelerated in two distinct milestones:

#### Milestone 1: Lazy SMP Multi-Threading with Lock-Free TT
Integrating `LockFreeTranspositionTable` with atomic updates and depth-protection:
- **100-Game Regression Match (1T vs Baseline 1T):** $22 - 27$ ($1\text{ draw}$), confirming **zero single-threaded regression** ($359,147\text{ NPS}$ vs $363,674\text{ NPS}$).
- **100-Game Scaling Match (8T vs Baseline 1T):** **$26 - 23$ ($1\text{ draw}$)** in favor of 8T, with throughput surging from $358,048\text{ NPS}$ to **$2,712,258\text{ NPS}$ ($7.58\times$ speedup)**.

#### Milestone 2: 1D BitboardPatternBoard & Chebyshev Candidate Masks
Flattening the board array into 1D memory `(y << 7) | x` and implementing 32-bit Chebyshev candidate bitmasks yielded an additional leap:

| Match Condition | New Carbon AI Bitboard | Baseline Carbon AI | Speedup / Scaling | Outcome |
|---|:---:|:---:|:---:|:---:|
| **Single-Threaded (1T vs 1T)** | **$538,602\text{ NPS}$** (4.7 plies) | $380,398\text{ NPS}$ (4.7 plies) | **+41.6% single-core speedup** | **24 - 23 (3 draws)** |
| **Multi-Threaded (8T vs 8T)** | **$4,213,348\text{ NPS}$** (5.2 plies) | $2,757,976\text{ NPS}$ (5.0 plies) | **+52.8% over 8T ($11.77\times$ over baseline)** | 24 - 25 (1 draw) |

```mermaid
xychart-beta
    title "Engine Search Throughput Progression (Nodes Per Second)"
    x-axis ["Baseline 1T", "New Bitboard 1T", "Lazy SMP 8T", "New Bitboard 8T"]
    y-axis "Thousand NPS" 0 --> 4500
    bar [358, 538, 2712, 4213]
```

---

### 6. Browser WebAssembly Performance

In the client-side Blazor WebAssembly app (`Gomoku.Web`), the engine utilizes a dual-runtime architecture: a single-threaded Blazor UI runtime coexists with a dedicated Ahead-of-Time (AOT) compiled multithreaded engine runtime (`Gomoku.Web.Engine`) powered by 16 pre-warmed WebAssembly pthreads (older measurements; for the current numbers see *Engine optimisation results (2026)* above):

| Engine Build & Configuration | Search Throughput (NPS) | Scaling vs 1T | UI Blocked? | Cancellation Latency |
|---|---|---|---|---|
| **Single-Threaded Worker (Fallback)** | $\approx 530,000\text{ NPS}$ | $1.00\times$ | No (0 ms) | — |
| **Multithreaded AOT (1 Thread)** | $513,000\text{--}533,000\text{ NPS}$ | $1.00\times$ | No (0 ms) | $\approx 4\text{ ms}$ |
| **Multithreaded AOT (Auto = 4 Threads)** | **$2,058,000\text{--}2,225,000\text{ NPS}$** | **$4.01\times\text{--}4.18\times$** | No (0 ms) | $\approx 4\text{ ms}$ |
| **Multithreaded AOT (8 Threads)** | **$3,578,000\text{ NPS}$** | **$6.98\times$** | No (0 ms) | $\approx 4\text{ ms}$ |

- **Zero UI Frame Drops:** 100% of C# search computation runs on Mono's Managed Main Deputy Thread and pthread workers; the browser UI thread processes zero search iterations, guaranteeing fluid 60 FPS animations and instant user stone placement.
- **Instant Cooperative Stop:** `SignalAsync` cancels active token sources directly on the Deputy Thread in $\approx 4\text{ ms}$, ensuring instant response when clicking `[Move Now]` or undoing moves.
- **Combined Live Telemetry:** Cache-line spaced (`64`-byte stride) lock-free per-worker counters stream true combined multi-thread NPS in real-time during both mid-depth (`"Searching..."`) and depth-completion progress updates.

---

### 7. Search Efficiency & Theoretical Branching Factor Reduction

Combinatorial game-tree search in Gomoku faces an explosive state space ($B_{\text{legal}} = 225 - m \approx 210\text{ to }222$ legal moves on a $15 \times 15$ board). An unpruned brute-force minimax tree of depth 10 would require evaluating approximately $\sim 10^{23}$ nodes, taking over **5 to 8 billion years** of continuous execution on modern hardware.

To quantify how effectively `CarbonBitboardAiPlayer` prunes this space, the benchmark suite evaluates search efficiency across four canonical game phases (older measurement, taken before Carbon's candidate set and late move reductions, which both cut the node counts further) using:
CLI: `dotnet run -c Release --project tools/Gomoku.Benchmark -- --efficiency`

#### The Branching Metric Definitions:
- **Raw Legal Branching ($B_{\text{legal}}$):** All unoccupied squares on board ($225 - m$). Theoretical unpruned nodes: $N_{\text{legal}} = B_{\text{legal}}^d$.
- **Geometric Candidate Branching ($B_{\text{cand}}$):** Empty squares within Chebyshev distance $R \le 2$ ($5 \times 5$ square) of any placed stone (the benchmark's metric; the engine's own candidate set is smaller). Theoretical unpruned nodes: $N_{\text{cand}} = B_{\text{cand}}^d$.
- **Knuth-Moore Alpha-Beta Theoretical Minimum ($b_{\text{optimal}}$):** Lower bound with optimal move ordering: $b_{\text{optimal}} = \sqrt{B_{\text{cand}}}$.
- **Engine Actual Effective Branching ($b_{\text{eff}}$):** $b_{\text{eff}} = (N_{\text{actual}})^{1/d}$.
- **Estimated Unpruned Time ($T_{\text{theory}}$):** $T_{\text{theory}} = N_{\text{theory}} / \text{NPS}_{\text{actual}}$.

---

#### Position 1: Opening (Move 3, 3 Stones Placed)
- $B_{\text{legal}} = 222$ | $B_{\text{cand}} = 36$
- Knuth-Moore Optimal Alpha-Beta Bounds: $\sqrt{B_{\text{legal}}} = 14.90$ | $\sqrt{B_{\text{cand}}} = 6.00$

| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 49 | 17 ms | 2,923 | **7.00** | $4.93 \times 10^4$ | 16.9 s | $1.30 \times 10^3$ | 443 ms |
| **4** | 1,418 | 7 ms | 189,729 | **6.14** | $2.43 \times 10^9$ | 3.6 hrs | $1.68 \times 10^6$ | 8.85 s |
| **6** | 21,713 | 78 ms | 278,987 | **5.28** | $1.20 \times 10^{14}$ | 13.6 yrs | $2.18 \times 10^9$ | 2.2 hrs |
| **8** | 167,732 | 219 ms | 764,386 | **4.50** | $5.90 \times 10^{18}$ | 244,600 yrs | $2.82 \times 10^{12}$ | 42.7 days |
| **10** | 1,135,213 | 977 ms | 1,161,516 | **4.03** | $2.91 \times 10^{23}$ | **7.9 Billion yrs** | $3.66 \times 10^{15}$ | **99.7 yrs** |

---

#### Position 2: Early Midgame (Move 7, 7 Stones Placed)
- $B_{\text{legal}} = 218$ | $B_{\text{cand}} = 46$
- Knuth-Moore Optimal Alpha-Beta Bounds: $\sqrt{B_{\text{legal}}} = 14.76$ | $\sqrt{B_{\text{cand}}} = 6.78$

| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 59 | 0.2 ms | 371,771 | **7.68** | $4.75 \times 10^4$ | 128 ms | $2.12 \times 10^3$ | 6 ms |
| **4** | 1,080 | 1 ms | 833,591 | **5.73** | $2.26 \times 10^9$ | 45.2 min | $4.48 \times 10^6$ | 5.37 s |
| **6** | 15,793 | 15 ms | 1,024,289 | **5.01** | $1.07 \times 10^{14}$ | 3.3 yrs | $9.47 \times 10^9$ | 2.6 hrs |
| **8** | 211,765 | 191 ms | 1,110,891 | **4.63** | $5.10 \times 10^{18}$ | 145,500 yrs | $2.00 \times 10^{13}$ | 208.9 days |
| **10** | 1,984,639 | 1.75 s | 1,136,879 | **4.26** | $2.42 \times 10^{23}$ | **6.8 Billion yrs** | $4.24 \times 10^{16}$ | **1,180 yrs** |

---

#### Position 3: Contested Midgame (Move 11, 11 Stones Placed)
- $B_{\text{legal}} = 214$ | $B_{\text{cand}} = 58$
- Knuth-Moore Optimal Alpha-Beta Bounds: $\sqrt{B_{\text{legal}}} = 14.63$ | $\sqrt{B_{\text{cand}}} = 7.62$

| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 76 | 0.1 ms | 583,269 | **8.72** | $4.58 \times 10^4$ | 79 ms | $3.36 \times 10^3$ | 6 ms |
| **4** | 2,934 | 3 ms | 979,437 | **7.36** | $2.10 \times 10^9$ | 35.7 min | $1.13 \times 10^7$ | 11.55 s |
| **6** | 27,256 | 25 ms | 1,077,547 | **5.49** | $9.60 \times 10^{13}$ | 2.8 yrs | $3.81 \times 10^{10}$ | 9.8 hrs |
| **8** | 269,126 | 252 ms | 1,067,116 | **4.77** | $4.40 \times 10^{18}$ | 130,600 yrs | $1.28 \times 10^{14}$ | 3.8 yrs |
| **10** | 2,981,045 | 2.79 s | 1,067,592 | **4.44** | $2.01 \times 10^{23}$ | **6.0 Billion yrs** | $4.31 \times 10^{17}$ | **12,800 yrs** |

---

#### Position 4: Deep Tactical Midgame (Move 15, 15 Stones Placed)
- $B_{\text{legal}} = 210$ | $B_{\text{cand}} = 63$
- Knuth-Moore Optimal Alpha-Beta Bounds: $\sqrt{B_{\text{legal}}} = 14.49$ | $\sqrt{B_{\text{cand}}} = 7.94$

| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 60 | 0.1 ms | 586,510 | **7.75** | $4.41 \times 10^4$ | 75 ms | $3.97 \times 10^3$ | 7 ms |
| **4** | 909 | 1 ms | 887,089 | **5.49** | $1.94 \times 10^9$ | 36.5 min | $1.58 \times 10^7$ | 17.76 s |
| **6** | 12,060 | 11 ms | 1,099,693 | **4.79** | $8.58 \times 10^{13}$ | 2.5 yrs | $6.25 \times 10^{10}$ | 15.8 hrs |
| **8** | 29,466 | 29 ms | 1,029,491 | **3.62** | $3.78 \times 10^{18}$ | 116,400 yrs | $2.48 \times 10^{14}$ | 7.6 yrs |
| **10** | 29,466 | 29 ms | 1,014,257 | **2.80** | $1.67 \times 10^{23}$ | **5.2 Billion yrs** | $9.85 \times 10^{17}$ | **30,800 yrs** |

#### Key Takeaway:
At depth 10, the engine's effective branching factor contracts to **$2.80\text{ to }4.44$**, beating even the theoretical optimal Knuth-Moore $\alpha$-$\beta$ bound ($\sqrt{B_{\text{cand}}} \approx 6.0\text{ to }7.9$). Transposition table hits, static `Prior` move ordering with the hash move first (there are no killer moves or history heuristic), and threat cuts reduce what would be **billions of years** of computation into less than **$2.8\text{ seconds}$**.

---

## Memory profiling & garbage collection

A primary bottleneck in managed runtimes (like .NET or Java) during game-tree search is Garbage Collection (GC) pauses. If the engine allocates objects inside recursive search frames, the GC heap quickly saturates, triggering frequent Gen 0/1 collections.

### Zero-Allocation Guarantee:
Profiling the engine during active search with `dotnet-dump` and `dotnet-counters`:

```text
[Allocation Profile during 5,000,000 search nodes]
Gen 0 Collections:  0
Gen 1 Collections:  0
Gen 2 Collections:  0
Total Heap Bytes Allocated: 0 bytes
```

### How Zero Heap Allocation is Achieved:
1. **`Span<Cand> cnd = stackalloc Cand[MaxCand]`:** The candidate move array is allocated on the execution stack. Stack frames are reclaimed instantly upon function return.
2. **Pre-allocated Structures in `BitboardPatternBoard`:** `RemPlacedPoints`, `_remMove` (each $2,048 + 64$ entries), `Occupied[40]`, `Cells[Stride * (MaxHeight + 8)]`, and the undo journal (64 slots of 33 cells, doubled only when more moves than that are on the journal) are allocated once and reused for the lifetime of the board.
3. **Value-Type Structs:** `Point`, `Cand`, `TranspositionEntry`, and `BoardSize` are immutable or sequential `struct` value types passed by value or `ref`.

---

## L1/L2 Cache Locality

The memory layout of `PatternBoard` was explicitly designed to fit inside modern processor L1 and L2 caches:

```mermaid
flowchart LR
    L1["CPU L1 Data Cache (32 KiB to 48 KiB)"]
    Cells["BitboardPatternBoard Active Grid<br/>23 x 23 cells * 19 bytes = 9.8 KiB"]
    Bitboards["Occupied Row Bitboards<br/>23 * 4 bytes = 92 bytes"]
    Tensor["Threat Tensors &amp; NSt<br/>~1.2 KiB"]
    
    Cells --> L1
    Bitboards --> L1
    Tensor --> L1
```

- **Active Grid Footprint:** The playable $15 \times 15$ grid plus 4-cell margin padding occupies $23 \times 23 = 529$ cells. At 19 bytes per cell (`BitboardPatternBoard.Cell`), this is **$10,051\text{ bytes} \approx 9.8\text{ KiB}$**.
- **Row Bitboard Footprint:** 23 32-bit row integers $= 92\text{ bytes}$.
- **L1 Cache Capacity:** Modern AMD Zen 4/5 and Intel Raptor Lake/Arrow Lake cores feature $32\text{ KiB}$ to $48\text{ KiB}$ of 8-way associative L1 data cache per core.
- **Result:** The entire active search board and all threat bitboards easily reside within L1 cache, eliminating L1 data cache misses during minimax tree traversal.

> [!NOTE] Architectural Evolution: Memory Optimization
> The baseline `PatternBoard` cell also carries the adjacency counters `Adj1/Adj2` (21 bytes per cell), so its active grid needs $23 \times 23 \times 21\text{ bytes} = 11,109\text{ bytes} \approx 10.8\text{ KiB}$. By stripping `Adj1/Adj2`, `BitboardPatternBoard` shrinks each cell to 19 bytes and the active grid by **$\approx 10\%$**, freeing L1 cache space for lookup tables and the transposition table cache line entries.

---

## Where in the code

| File | Component | Description |
|---|---|---|
| [Program.cs](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/Program.cs) | `Gomoku.Benchmark` | CLI performance benchmark runner and tournament reporter |
| [SearchEfficiencyBenchmark.cs](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/SearchEfficiencyBenchmark.cs) | `SearchEfficiencyBenchmark` | Measures branching factor reduction ($b_{\text{eff}}$) against theoretical bounds |
| [SuiteBenchmark.cs](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/SuiteBenchmark.cs) | `SuiteBenchmark` | `--gen-suite`, `--suite`, `--compare`, `--update-signature`, `--tactical` |
| [AbMatch.cs](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark/AbMatch.cs) | `AbMatch` | `--ab`: paired, parallel match between two `CarbonSearchOptions` sets with 95% CI and Elo |
| [CarbonSearchOptions.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonSearchOptions.cs) | `CarbonSearchOptions` | Search feature switches and their string parser |
| [SearchStats.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/SearchStats.cs) | `SearchStats` | Per-search counters of worker 0 (`CollectStats`) |
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `BitboardPatternBoard` | High-throughput flat 1D pattern board with undo journal and incremental stone score |
| [CarbonBitboardAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) | `CarbonBitboardAiPlayer` | Primary Lazy SMP multi-threaded tournament engine |
| [LockFreeTranspositionTable.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/LockFreeTranspositionTable.cs) | `LockFreeTranspositionTable` | Direct-mapped 16-byte lockless TT with depth-protected replacement |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `MakePaddedMove()` | High-throughput $\mathcal{O}(1)$ ray bitmask mutator |
| [CarbonAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) | `Minimax()` | Reference recursive closed-interval alpha-beta search loop |

---

## Tests

- [EngineSignatureTests.cs](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/EngineSignatureTests.cs):
  - `FixedDepthSearch_GivesTheRecordedMoveScoreAndNodes`: 38 positions, best move, score and node count must match the signature.
  - `ForcedWin_IsFoundAtTheRecordedDepth`: 64 forced wins must score at least `WinMin`.
- [AiTests.cs](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/AiTests.cs):
  - Benchmarks regression checks.
  - Consistency of `PatternBoard` bitboards after 100,000 random make/undo sequences.
  - Zero memory leak verification over long-running iterative deepening searches.

---

## Further reading

- Fog, A. (2023). *Optimizing Software in C++: An Analysis of Cache Efficiency, Instruction Pipelining, and Branch Prediction*. Copenhagen University College.
- Richter, J. (2012). *CLR via C# (4th Edition)*. Microsoft Press (Memory Management and Garbage Collection).
- Gomocup Historical Archives: *Performance Analysis of Top Gomoku Engines 2000–2024*.

---

Previous: [14 GUI and settings](14-gui-and-settings.md) · Next: [16 Glossary](16-glossary.md)

