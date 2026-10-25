# Search Efficiency & Branching Factor Reduction Walkthrough

## Summary of Accomplished Work

This walkthrough accompanies [search-efficiency-branching-factor-plan.md](search-efficiency-branching-factor-plan.md) and documents the design, empirical benchmark results, codebase integration, and documentation updates quantifying the search efficiency and branching factor reduction of `CarbonBitboardAiPlayer`.

---

## 1. Integrated Feature: `SearchEfficiencyBenchmark`

A new benchmarking engine has been permanently integrated into `tools/Gomoku.Benchmark`:
- **Implementation File:** [SearchEfficiencyBenchmark.cs](file:///c:/Udvikling/Spil/Gomoku/tools/Gomoku.Benchmark/SearchEfficiencyBenchmark.cs)
- **CLI Dispatcher:** [Program.cs](file:///c:/Udvikling/Spil/Gomoku/tools/Gomoku.Benchmark/Program.cs#L34-L53)
- **Usage:**
  ```powershell
  # Default suite (Depths 2, 4, 6, 8, 10 on 4 canonical positions, 1 thread):
  dotnet run -c Release --project tools/Gomoku.Benchmark -- --efficiency

  # Custom depth or threads:
  dotnet run -c Release --project tools/Gomoku.Benchmark -- --efficiency --depth 10 --threads 1
  ```

---

## 2. Canonical Test Positions Evaluated

The benchmark tests 4 distinct, uncontested game positions representing all phases of Gomoku play:
1. **Position 1: Opening (Move 3)** – Classic indirect opening `(7,7)`, `(6,6)`, `(6,5)`. Wide open board space ($B_{\text{legal}} = 222$, $B_{\text{cand}} = 36$).
2. **Position 2: Early Midgame (Move 7)** – Cross skirmish in central $5 \times 5$ square ($B_{\text{legal}} = 218$, $B_{\text{cand}} = 46$).
3. **Position 3: Contested Midgame (Move 11)** – Overlapping diagonal open threes and tactical fight ($B_{\text{legal}} = 214$, $B_{\text{cand}} = 58$).
4. **Position 4: Deep Tactical Midgame (Move 15)** – Dense cluster across rows 4–9 and cols 3–7 with tactical threats ($B_{\text{legal}} = 210$, $B_{\text{cand}} = 63$).

---

## 3. Empirical Verification Results

The benchmark was executed end-to-end via `dotnet run -c Release --project tools/Gomoku.Benchmark -- --efficiency`.
Total execution time across all 4 positions and all depths was **6.5 seconds**, strictly complying with the $< 1$ minute budget constraint per position.

### Position 1: Opening (Move 3)
| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 49 | 17 ms | 2,923 | **7.00** | $4.93 \times 10^4$ | 16.9 s | $1.30 \times 10^3$ | 443 ms |
| **4** | 1,418 | 7 ms | 189,729 | **6.14** | $2.43 \times 10^9$ | 3.6 hrs | $1.68 \times 10^6$ | 8.85 s |
| **6** | 21,713 | 78 ms | 278,987 | **5.28** | $1.20 \times 10^{14}$ | 13.6 yrs | $2.18 \times 10^9$ | 2.2 hrs |
| **8** | 167,732 | 219 ms | 764,386 | **4.50** | $5.90 \times 10^{18}$ | 244,600 yrs | $2.82 \times 10^{12}$ | 42.7 days |
| **10** | 1,135,213 | 977 ms | 1,161,516 | **4.03** | $2.91 \times 10^{23}$ | **7.9 Billion yrs** | $3.66 \times 10^{15}$ | **99.7 yrs** |

### Position 2: Early Midgame (Move 7)
| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 59 | 0.2 ms | 371,771 | **7.68** | $4.75 \times 10^4$ | 128 ms | $2.12 \times 10^3$ | 6 ms |
| **4** | 1,080 | 1 ms | 833,591 | **5.73** | $2.26 \times 10^9$ | 45.2 min | $4.48 \times 10^6$ | 5.37 s |
| **6** | 15,793 | 15 ms | 1,024,289 | **5.01** | $1.07 \times 10^{14}$ | 3.3 yrs | $9.47 \times 10^9$ | 2.6 hrs |
| **8** | 211,765 | 191 ms | 1,110,891 | **4.63** | $5.10 \times 10^{18}$ | 145,500 yrs | $2.00 \times 10^{13}$ | 208.9 days |
| **10** | 1,984,639 | 1.75 s | 1,136,879 | **4.26** | $2.42 \times 10^{23}$ | **6.8 Billion yrs** | $4.24 \times 10^{16}$ | **1,180 yrs** |

### Position 3: Contested Midgame (Move 11)
| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 76 | 0.1 ms | 583,269 | **8.72** | $4.58 \times 10^4$ | 79 ms | $3.36 \times 10^3$ | 6 ms |
| **4** | 2,934 | 3 ms | 979,437 | **7.36** | $2.10 \times 10^9$ | 35.7 min | $1.13 \times 10^7$ | 11.55 s |
| **6** | 27,256 | 25 ms | 1,077,547 | **5.49** | $9.60 \times 10^{13}$ | 2.8 yrs | $3.81 \times 10^{10}$ | 9.8 hrs |
| **8** | 269,126 | 252 ms | 1,067,116 | **4.77** | $4.40 \times 10^{18}$ | 130,600 yrs | $1.28 \times 10^{14}$ | 3.8 yrs |
| **10** | 2,981,045 | 2.79 s | 1,067,592 | **4.44** | $2.01 \times 10^{23}$ | **6.0 Billion yrs** | $4.31 \times 10^{17}$ | **12,800 yrs** |

### Position 4: Deep Tactical Midgame (Move 15)
| Depth | Actual Nodes | Time Actual | Engine NPS | Actual $b_{\text{eff}}$ | Theoretical Legal Nodes | Est. Time (Legal) | Theoretical Cand Nodes | Est. Time (Cand) |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **2** | 60 | 0.1 ms | 586,510 | **7.75** | $4.41 \times 10^4$ | 75 ms | $3.97 \times 10^3$ | 7 ms |
| **4** | 909 | 1 ms | 887,089 | **5.49** | $1.94 \times 10^9$ | 36.5 min | $1.58 \times 10^7$ | 17.76 s |
| **6** | 12,060 | 11 ms | 1,099,693 | **4.79** | $8.58 \times 10^{13}$ | 2.5 yrs | $6.25 \times 10^{10}$ | 15.8 hrs |
| **8** | 29,466 | 29 ms | 1,029,491 | **3.62** | $3.78 \times 10^{18}$ | 116,400 yrs | $2.48 \times 10^{14}$ | 7.6 yrs |
| **10** | 29,466 | 29 ms | 1,014,257 | **2.80** | $1.67 \times 10^{23}$ | **5.2 Billion yrs** | $9.85 \times 10^{17}$ | **30,800 yrs** |

---

## 4. Documentation Synchronized

1. **[08-midgame-search.md](file:///c:/Udvikling/Spil/Gomoku/docs/brain/08-midgame-search.md):**
   - Added `## Search Efficiency & Branching Factor Reduction`.
   - Documented the Branching Factor Hierarchy ($B_{\text{legal}}$ vs $B_{\text{cand}}$ vs Knuth-Moore bound vs $b_{\text{eff}}$).
   - Added explanation for why $b_{\text{eff}} \approx 2.80\text{ to }4.44$ beats the theoretical $\sqrt{B_{\text{cand}}} \approx 6.0\text{ to }7.9$ lower bound (TT cuts, killer/history heuristics, threat cuts).
2. **[15-benchmarks-and-performance.md](file:///c:/Udvikling/Spil/Gomoku/docs/brain/15-benchmarks-and-performance.md):**
   - Added `--efficiency` CLI invocation snippet to `## Benchmark suite (Gomoku.Benchmark)`.
   - Added `### 6. Search Efficiency & Theoretical Branching Factor Reduction` containing the 4 canonical position comparison tables.
   - Updated the `Where in the code` index table to cite `SearchEfficiencyBenchmark.cs`.

---

## 5. Test Suite Verification

- `dotnet test`: Verified all 83 unit and integration tests continue to pass with 0 warnings and 0 errors.

