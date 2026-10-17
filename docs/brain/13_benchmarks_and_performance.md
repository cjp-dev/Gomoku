# Chapter 13: Benchmarks & Empirical Performance

## 13.1 Benchmarking Methodology
Performance validation is performed using the dedicated `Gomoku.Benchmark` CLI runner targeting .NET 10 (Release configuration, Tiered PGO enabled).

The suite measures three distinct architectural subsystems:
1. **Move Generation & Pattern Updates (`PatternBoard`)**
2. **Transposition Table Throughput & Cache Contention**
3. **Full Carbon AI Minimax Search Speed (Nodes per Second)**

---

## 13.2 Empirical Results

### 1. PatternBoard Move / Undo Operations
- **Task:** Repeated placement and unmaking of 3-move sequences on a $15 \times 15$ board.
- **Operations:** 60,000 make/undo cycles.
- **Elapsed Time:** 147 ms.
- **Throughput:** **407,000 operations / second**.
- **Consistency Verification:** `CheckConsistency()` confirmed that all threat tensors `nSt[player, grade]` matched scratch board counts after 60,000 undos.

### 2. Transposition Table Masking & Probing
- **Task:** Sequential and pseudorandom store/probe operations simulating search transpositions.
- **Small Table ($2^{16} = 65,536$ entries / $1 \text{ MiB}$):**
  - 200,000 operations in 8 ms $\implies$ **22,448,000 ops / sec**.
- **Standard Table ($2^{20} = 1,048,576$ entries / $16 \text{ MiB}$):**
  - 200,000 operations in 5 ms $\implies$ **39,544,000 ops / sec**.
- **Conclusion:** Power-of-two bitwise masking (`& (count - 1)`) and 16-byte aligned entries ensure zero cache penalty even with over 1 million slots.

### 3. Carbon AI Minimax Search Speed
- **Opening:** 3-ply standard center opening ($H8, H9, I8$).
- **$15 \times 15$ Board (Depth 4 plies):**
  - Solved in **12 milliseconds**.
  - Evaluated **906 nodes**.
- **$19 \times 19$ Board (Depth 4 plies):**
  - Solved in **7 milliseconds**.
  - Evaluated **906 nodes**.
- **Conclusion:** Identical node count proves that board dimension does not enlarge the search tree when bounding-box candidate pruning is active!

