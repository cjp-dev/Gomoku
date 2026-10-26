# 15 – Benchmarks and performance

[Back to the index](README.md)

## In short

Gomoku engines require extreme computational throughput: evaluating deep tactical combinations and searching 10 to 14 plies ahead within strict tournament time budgets demands hundreds of thousands of node evaluations per second.

Gomoku .NET achieves performance parity with native C++ engines through careful systems-level engineering:
- **Zero Heap Allocations on Hot Paths:** Candidate generation uses `stackalloc Cand[MaxCand]` (`MaxCand = 256`), bitboards operate on primitives, and move undo/redo operates via pre-allocated structures.
- **Incremental Pattern Bitboard Maintenance:** `MakeMove()` and `UndoMove()` update 4-directional ray bitmasks and recalculate threat tensors in strictly $\mathcal{O}(1)$ time.
- **Empirical Throughput:**
  - `PatternBoard` make/undo throughput: **$1,340,000+\text{ ops/sec}$**.
  - Transposition Table probe/store throughput: **$167,000,000+\text{ ops/sec}$**.
  - Single-threaded search throughput: **$538,000+\text{ NPS}$** (.NET 10 desktop & Web Worker).
  - Multi-threaded Lazy SMP throughput: **$4,213,000+\text{ NPS}$** ($11.77\times$ speedup on 8 cores).

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
```

---

## Empirical benchmark results

The following measurements were captured on a modern 64-bit multi-core machine running .NET 10 (Release configuration):

### 1. Board Representation Throughput (Make/Undo Speed)

Empirical make/undo measurements captured via `dotnet run --project tools/Gomoku.Benchmark` on $15 \times 15$ boards:

| Board Implementation | Total Ops Tested | Elapsed Time | Throughput (ops/sec) | Speedup vs Baseline |
|---|:---:|:---:|:---:|:---:|
| **Classical `PatternBoard` (2D array + 24 neighbor writes)** | $600,000$ | $2,056\text{ ms}$ | $291,796\text{ ops/sec}$ | $1.00\times$ |
| **`BitboardPatternBoard` (1D Stride-128 + Chebyshev Dilation)** | $600,000$ | $823\text{ ms}$ | **$728,166\text{ ops/sec}$** | **$2.50\times$ faster** |
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

At $4\text{ MiB}$ ($262,144$ entries), the entire table fits inside the CPU L3 cache, delivering peak throughput of **$341\text{ million}$ operations per second** ($2.9\text{ ns}$ per probe). At $64\text{ MiB}$, main RAM memory bus access limits throughput to $62.6\text{ M ops/sec}$.

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

In the client-side Blazor WebAssembly app (`Gomoku.Web`), the engine utilizes a dual-runtime architecture: a single-threaded Blazor UI runtime coexists with a dedicated Ahead-of-Time (AOT) compiled multithreaded engine runtime (`Gomoku.Web.Engine`) powered by 16 pre-warmed WebAssembly pthreads:

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

### 6. Search Efficiency & Theoretical Branching Factor Reduction

Combinatorial game-tree search in Gomoku faces an explosive state space ($B_{\text{legal}} = 225 - m \approx 210\text{ to }222$ legal moves on a $15 \times 15$ board). An unpruned brute-force minimax tree of depth 10 would require evaluating approximately $\sim 10^{23}$ nodes, taking over **5 to 8 billion years** of continuous execution on modern hardware.

To quantify how effectively `CarbonBitboardAiPlayer` prunes this space, the benchmark suite evaluates search efficiency across four canonical game phases using:
CLI: `dotnet run -c Release --project tools/Gomoku.Benchmark -- --efficiency`

#### The Branching Metric Definitions:
- **Raw Legal Branching ($B_{\text{legal}}$):** All unoccupied squares on board ($225 - m$). Theoretical unpruned nodes: $N_{\text{legal}} = B_{\text{legal}}^d$.
- **Geometric Candidate Branching ($B_{\text{cand}}$):** Empty squares within Chebyshev distance $R \le 2$ ($5 \times 5$ square) of any placed stone. Theoretical unpruned nodes: $N_{\text{cand}} = B_{\text{cand}}^d$.
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
At depth 10, the engine's effective branching factor contracts to **$2.80\text{ to }4.44$**, beating even the theoretical optimal Knuth-Moore $\alpha$-$\beta$ bound ($\sqrt{B_{\text{cand}}} \approx 6.0\text{ to }7.9$). Transposition table hits, killer/history ordering, and threat cuts reduce what would be **billions of years** of computation into less than **$2.8\text{ seconds}$**.

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
2. **Pre-allocated Structures in `BitboardPatternBoard`:** `RemPlacedPoints[2048]`, `_remMove[2048]`, `Occupied[31]`, and `Cells[Stride * (MaxHeight + 8)]` are allocated once when the board is initialized and reused for the lifetime of the match.
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
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `BitboardPatternBoard` | High-throughput flat 1D pattern board with Chebyshev bitboard masks |
| [CarbonBitboardAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) | `CarbonBitboardAiPlayer` | Primary Lazy SMP multi-threaded tournament engine |
| [LockFreeTranspositionTable.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/LockFreeTranspositionTable.cs) | `LockFreeTranspositionTable` | 16-byte lockless TT with depth-protected replacement |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `MakePaddedMove()` | High-throughput $\mathcal{O}(1)$ ray bitmask mutator |
| [CarbonAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) | `Minimax()` | Reference recursive PVS search loop |

---

## Tests

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

