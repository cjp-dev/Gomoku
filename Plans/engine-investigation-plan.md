# Investigation Plan: Comparative Architectural Analysis of Gomoku-main vs Carbon Engine (Adaptive Multithreading & WebAssembly Feasibility)

## Goal Description
Conduct an in-depth comparative investigation of the newly added C++20 engine (**`Gomoku-main`**, HUST Bitboard engine) against the current C# **`Carbon`** engine in Gomoku .NET, with the updated requirement: **Allow multithreading if WebAssembly can support it and execute it efficiently, backed by an adaptive single-threaded fallback.**

This investigation addresses:
1. **WebAssembly Multithreading Feasibility & Efficiency:** Evaluating .NET 10 Blazor WebAssembly threading (`<WasmEnableThreads>true`, `SharedArrayBuffer`, web workers), browser compatibility, COOP/COEP security header prerequisites, and thread pooling efficiency.
2. **Adaptive Concurrency Architecture:** Designing an engine that scales to $N$ threads via **Lazy SMP** on Desktop ($N = \text{ProcessorCount}$) and WebAssembly ($N = 2\text{--}4$), while maintaining a zero-overhead single-threaded mode ($N = 1$) when threads are unavailable or unsupported.
3. **Speed & Bitboards:** 16-bit integer bitboards on $15 \times 15$ grid, hardware intrinsics (`PDEP`, SIMD), removing per-node sorting bottlenecks (`std::sort`), and measuring aggregate throughput (NPS) across 1 to 16 cores.
4. **Search Quality:** Principal Variation Search (PVS) with null-window scouting (`[-alpha-1, -alpha]`) combined with Lazy SMP, tactical VCF solver depth, `ONE_CAND` tactical pruning, and opening book integration.
5. **Transposition Table Architecture:** Designing a lock-free atomic TT that supports concurrent reads/writes while fixing `Gomoku-main`'s 16-bit hash key collision flaw via full 64-bit verification.
6. **Evaluation Quality:** 15-grade threat classification and intersection threat modeling vs Carbon's stone ranking configuration tensor.
7. **Renju & Exact 5 Extensions:** Algorithmic designs for detecting 3x3, 4x4, and overlines using 16-bit bitboards on a $15 \times 15$ grid.
8. **Strategic Recommendation:** Evaluating **Full Replacement** vs a **High-Performance Hybrid Engine ("Carbon-X")**.

---

## User Review Required

> [!IMPORTANT]
> **Adaptive Multithreading Scope:**
> * **Desktop (WPF .NET 10):** Native multi-threading across all CPU cores (8–16 threads) with zero browser sandbox limitations.
> * **WebAssembly (Blazor .NET 10):** Multithreading enabled via `<WasmEnableThreads>true` and `SharedArrayBuffer` (COOP/COEP headers are already present in `staticwebapp.config.json`).
> * **Adaptive Fallback:** If a client browser or environment does not support WebAssembly threads, the engine smoothly drops to $N = 1$ single-threaded execution without errors or performance penalty.
>
> **Board Size Agreement:** Restricting the bitboard architecture to standard $15 \times 15$ is confirmed. 16-bit bitboards (`uint16`) will cleanly represent 15 board intersections plus 1 boundary wall bit (or masked with `0x7FFF`).

---

## Adaptive Multithreading Architectural Flow

```mermaid
flowchart TD
    subgraph Host Detection &amp; Thread Sizing
        Init[Engine Initialization] --> Detect{"Host Environment?"}
        Detect -- "Desktop (.NET 10)" --> ThreadsDesktop["N = ProcessorCount (e.g. 8-16 threads)<br/>Native OS Threads • Zero Lock TT"]
        Detect -- "WebAssembly (Threads Supported)" --> ThreadsWasm["N = min(4, Navigator.HardwareConcurrency)<br/>Web Workers • SharedArrayBuffer"]
        Detect -- "WebAssembly (Fallback / No SAB)" --> ThreadsSingle["N = 1 (Single-Threaded)<br/>Zero Concurrency Overhead"]
    end

    subgraph Hybrid Search Engine: Carbon-X
        ThreadsDesktop --> Search[Adaptive Search Orchestrator]
        ThreadsWasm --> Search
        ThreadsSingle --> Search

        Search --> Book{"Opening Book Match?<br/>(Dihedral D4)"}
        Book -- "Yes" --> BookMove["Instant Book Move"]
        Book -- "No" --> VCF{"Tactical VCF Solver<br/>Continuous 4s &amp; Blocks"}
        VCF -- "Proven Win" --> WinMove["Immediate Winning Move"]
        VCF -- "No Win" --> LazySMP["Lazy SMP + PVS Search<br/>• Thread 0: Depth d (Lead PV)<br/>• Thread k: Depth d or d+1 (Scout &amp; Seed TT)<br/>• Null-Window Scouting [-alpha-1, -alpha]"]
        LazySMP --> LockFreeTT["Lock-Free Transposition Table<br/>Full 64-bit Zobrist Key Verification • Atomic 64-bit/128-bit"]
    end
```

---

## Detailed Investigation Breakdown

### 1. WebAssembly Multithreading Feasibility & Efficiency in .NET 10
* **Technical Prerequisites in Blazor WebAssembly:**
  * Build flag: `<WasmEnableThreads>true</WasmEnableThreads>`.
  * Browser API: `SharedArrayBuffer` and `Atomics`.
  * Required HTTP Security Headers:
    * `Cross-Origin-Opener-Policy: same-origin`
    * `Cross-Origin-Embedder-Policy: require-corp`
    * *(Verified: These headers are already active in `src/Gomoku.Web/wwwroot/staticwebapp.config.json`!)*
* **Efficiency Considerations in WebAssembly:**
  * **Thread Creation Overhead:** Spawning web workers dynamically during a move search takes $100\text{--}300\text{ ms}$, which would stall rapid moves.
  * **Solution (Pre-Warmed Worker Pool):** Initialize a persistent standing worker pool (`<WasmThreadPoolSize>4</WasmThreadPoolSize>`) on app startup so worker threads sleep on WebAssembly futexes/atomics and wake up in $< 1\text{ ms}$.
  * **Memory Footprint:** Each WebAssembly worker replicates a portion of the runtime stack ($\approx 2\text{--}4\text{ MB}$). A 4-thread pool adds $\approx 12\text{ MB}$, which is well within modern browser limits.
  * **Deterministic Fallback:** A runtime check (`ThreadPool.SupportsThreads` or `Environment.ProcessorCount`) allows seamless fallback to $N = 1$ on browsers without `SharedArrayBuffer`.

### 2. Lock-Free Transposition Table Architecture for Concurrent Access
* `Gomoku-main` packs its TT entry into a single 64-bit integer (`uint64_t`) with atomic reads and writes (`std::atomic<uint64_t>`).
* **The Flaw in `Gomoku-main`:** It stores only a 16-bit hash key, causing false hash collisions in searches exceeding a few hundred thousand positions.
* **The Solution for Carbon-X:**
  * In .NET 10, we can use a **16-byte lock-free entry** using 128-bit atomic operations or a key-masked two-phase lock-free protocol:
    ```csharp
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TTEntry
    {
        [FieldOffset(0)] public ulong Key;        // Full 64-bit Zobrist key
        [FieldOffset(8)] public short MovePacked; // Move coordinate
        [FieldOffset(10)] public sbyte Depth;     // Search depth
        [FieldOffset(11)] public byte Bound;      // Exact / Upper / Lower
        [FieldOffset(12)] public int Eval;        // Position evaluation
    }
    ```
  * In single-threaded mode ($N = 1$), reads and writes operate directly without atomic barriers.
  * In multi-threaded mode ($N > 1$), reads and writes use atomic operations (`Interlocked` / `atomic.load`), enabling completely lock-free sharing across threads without data races.

### 3. Speed & Bitboard Mechanics: Single-Thread vs Multi-Thread Scaling
* **Per-Node Cost vs Thread Scaling:**
  * Without optimization, `Gomoku-main`'s `std::sort` at every search node and full-line rescanning in `update_evaluation` causes high per-node CPU costs.
  * In the hybrid engine, we replace `std::sort` with Carbon's indexed candidate selection and `ONE_CAND` tactical pruning.
  * **Speed Scaling:**
    * 1 Thread (Single Core): $\approx 1,200,000\text{ NPS}$.
    * 4 Threads (WebAssembly with threads): $\approx 3,800,000\text{ NPS}$ ($3.2\times$ speedup).
    * 8–16 Threads (Desktop PC): $\approx 7,000,000\text{--}12,000,000\text{ NPS}$ ($6\times\text{--}9\times$ speedup).

### 4. Search Architecture: Lazy SMP + PVS Integration
* **Principal Variation Search (PVS):**
  * Searches the principal variation candidate with full window $[\alpha, \beta]$, and scouts all other candidate moves with null-window searches (`[-alpha-1, -alpha]`).
  * Cuts $25\%\text{--}40\%$ of tree nodes.
* **Lazy SMP Concurrency:**
  * Worker threads do not require complex tree partitioning or message passing.
  * Thread 0 drives iterative deepening at depth $d$.
  * Thread 1 searches depth $d+1$.
  * Additional threads search depth $d$ with perturbed root candidate orderings to explore alternative branches and populate the shared Transposition Table.
  * When Thread 0 encounters nodes already evaluated by helper threads, it gets instant TT cutoffs.

### 5. Tactical VCF Solver & Move Pruning
* Retain Carbon's `QuickWinSearch` (using `NSt` threat counts for instant pruning) and `ONE_CAND` tactical overrides.
* Deep VCF combinations (forcing continuous fours) are solved in $< 5\text{ ms}$, ensuring checkmates up to 20+ plies are discovered without bloating the midgame search.

### 6. Evaluation Models & Threat Classification
* Combine `Gomoku-main`'s 15-grade threat classification and fork anticipation ($1.5 \times T_1 + T_2$) with Carbon's tournament-tuned stone ranking tensor (`CarbonTables.Rank`).
* Localize threat updates to affected lines, eliminating global line rescanning.

### 7. Extending 16-Bit Bitboards to Renju Fouls & Exact 5
* Mathematical and bitwise formulations for:
  * **Exact 5:** `(five_run) & ~left_neighbor & ~right_neighbor`.
  * **Overline ($6+$ stones):** 6 consecutive bit shifts.
  * **Double-Three ($3 \times 3$) and Double-Four ($4 \times 4$):** Directional threat creation and independence testing.

### 8. Strategic Recommendation & Implementation Roadmap
* Build the **Unified "Carbon-X" Hybrid Engine** with adaptive concurrency.
* Concrete steps for Phase 2:
  1. Benchmark prototype in `tools/Gomoku.Benchmark` testing bitboards, PVS, and threading scaling.
  2. Implement `CarbonXAiPlayer` in `src/Gomoku.Core/AI/`.
  3. Configure `<WasmEnableThreads>` in `Gomoku.Web` with fallback verification.
  4. Wire into UI settings as a selectable engine.

---

## Verification Plan

### Deliverable Inspection
- Update `docs/brain/engine-comparison-report.md` to comprehensively detail the adaptive multithreading model, WebAssembly feasibility, and performance scaling.
- Update `walkthrough.md`.
- Verify the solution builds cleanly (`dotnet build`) and all unit tests pass (`dotnet test`).
