# Engine speed investigation: report

Date: 2026-10-27.
Engine: `CarbonBitboardAiPlayer` on `BitboardPatternBoard`.

This report records what was tried, how it was measured, and what was kept. Every kept change is its own commit, with the numbers in the commit message.

## Result

| | Before | After | Change |
|---|--:|--:|--:|
| Suite: 38 positions, fixed depth, 1 thread | 19.5 s | 5.85 s | **−70% (3.3× faster)** |
| Suite nodes | 14.9M | 6.5M | −56% |
| Nodes per second (1 thread, suite) | 764k | 1.11M | +45% |
| Browser (WASM AOT), depth 12, 1 thread | 55.1 s | 4.3 s | **12.8× faster** |
| Browser (WASM AOT), depth 12, 4 threads | 29.0 s | 3.2 s | **9× faster** |
| Strength: the two search changes, same speed (0.2 s/move, 730 games) | | | **+57 Elo** [+32, +82] |
| Strength: total, old search at its old speed (0.2 vs 0.138 s/move, 730 games) | | | **+88 Elo** [+63, +115] |

The browser runs measure the computer's first reply after H8 with the book off. Both engines played the same move.

### Why the browser gained 12.8× and the suite 3.3×

The difference comes mostly from the position, not from WASM. Running the same position (reply to H8, Freestyle, depth 12, 1 thread) with the desktop build gives 30.3 s before and 2.73 s after: **11.1×**. The time saved is the product of fewer nodes and more nodes per second:

| | Fewer nodes | Higher NPS | Total |
|---|--:|--:|--:|
| Suite, desktop (38 positions, depths 5–10) | 14.9M → 6.5M = 2.3× | 764k → 1.11M = 1.45× | **3.3×** |
| Reply to H8, depth 12, desktop | 29.6M → 5.0M = 5.9× | 975k → 1.83M = 1.88× | **11.1×** |
| Reply to H8, depth 12, browser | 29.6M → 5.0M = 5.9× | 536k → 1.15M = 2.15× | **12.8×** |

The browser and the desktop count exactly the same nodes.

- **The reply to H8 saves far more nodes (5.9× against 2.3×).** Both search changes work best in a quiet, deep position.
  - Late move reductions only shorten quiet moves. With one stone on the board almost every move is quiet, whereas the suite's mid-game positions (8–26 stones) are full of threats, which are never shortened.
  - The saving of late move reductions also grows with depth. A reduced move cuts 2 plies off its whole subtree, and this repeats at every level, so depth 12 gains more than the suite's typical depth 8.
  - Carbon's candidate set removes the most moves on an almost empty board. Later in the game the old 5×5 squares around the stones overlap, so the extra moves matter less.
- **The reply to H8 also gains more per node (1.88× against 1.45×).** This was not profiled. A likely cause is that late move reductions move more of the work into cheap searches close to the leaves, and this position uses them the most.
- **WASM adds only about 14% (2.15× against 1.88× per node).** A plausible reason, not measured, is that the speed-ups removed work that costs more under WASM AOT, such as calls through a delegate in the sort.

So 3.3× is the typical gain over many kinds of positions, and 12.8× is close to the best case: a quiet opening searched deep.

## How it was measured

The harness lives in `tools/Gomoku.Benchmark`. The full command list is in its `Program.cs`.

- **`--gen-suite`** plays deterministic self-play games in Freestyle, Exact5 and Renju. From them it writes:
  - `suite-positions.txt`: 38 positions, each with a depth chosen so the search takes about 1 s;
  - `tests/Gomoku.Core.Tests/TestData/engine-signature.txt`: best move, score and node count at a shallow depth;
  - `tactical-positions.txt`: 64 forced wins.
- **`EngineSignatureTests`** checks both files in every `dotnet test`. A change that should not change the search fails a test if it does.
- **`--suite`** runs the 38 positions at 1 thread with a fresh table each time. `--options` picks the search features; `--compare` and `--json` compare runs.
- **`--tactical --options …`** checks the forced wins with given options.
- **`--ab`** plays a paired match between two option sets in the same build. Each opening is played twice with the colours swapped, and games run in parallel. It reports the score with a 95% interval and Elo. There are 365 openings, so up to 730 games.
- **`CollectStats`** fills `SearchStats`: TT hits and cutoffs, leaves, first-move cutoffs and candidates.
- **`speedscope_top.py`** summarises a `dotnet-trace` profile.

**Noise.** This machine is a VM on a laptop, and two identical runs differed by up to 5%. The gate therefore compares builds with **interleaved passes**: the accepted build and the candidate alternate, and the comparison uses the sum of each position's *minimum* time. An A/A test of that method still showed 1.3%, so Tier A changes had to save **≥ 3%**.

**Gates.**
- **Tier A** must play exactly the same: the signature is unchanged and the suite is ≥ 3% faster.
- **Tier B** must find all forced wins, cut time to depth by ≥ 10%, and score ≥ 50% in a 730-game match at 1 thread and 0.2 s/move. An 8-thread match of 300 games confirms it.
- **A/A match check:** default against default scored 48.6% ± 4.9%, as expected.

## Profile of the starting point (self time)

| Function | Share |
|---|--:|
| `MakePaddedMove` + `UndoMove` | 52% |
| `GenerateCand` | 12% |
| `Evaluate` (loop over all stones at every leaf) | 12% |
| Sort with a delegate comparer | 8% |
| `Minimax` itself | 7% |
| `QuickWinSearch` | 1.6% |

## Kept

| # | Change | Commit | Measured |
|---|---|---|---|
| A4a | Undo from a journal: make saves the 33 cells it changes and the counters; undo copies them back | `68b3cdd` | −19.5%, same search |
| A2 | Incremental evaluation: running stone score per colour, so `Evaluate` is O(1) | `21d5950` | −7.4% on top of A4a, same search |
| A4b | Leaner make: unrolled directions, table refs, counters only on a change | `23f5e85` | −15.9%, same search |
| A5 | Introsort copy with an inlined comparison, so the order is unchanged | `47e069f` | −3.0%, same search |
| A1 | Removed the shared `Interlocked` hit/store counters in the TT | `b7db982` | within noise at 1 and 8 threads; kept because it removes a write that all threads share |
| B6 | Carbon's own candidate set: 3×3 ring plus 2 steps along the lines, priority +1 only next to a stone. The port used the full 5×5 square with +1 everywhere | `bedf767` | −14% nodes, −10% time; **+33 Elo** [+9, +59] at 1 thread; +23 [−16, +63] at 8 threads |
| B4 | Late move reductions: quiet late moves get a zero-width search 2 plies shallower, then a normal search if they beat the best move | `c5d0a82` | −49% nodes, −45% time; **+48 Elo** [+24, +74] at 1 thread; +30 [−9, +70] at 8 threads; +12 [−27, +50] at 1 s/move |

A2 was first tried without the journal. It was **3.4% slower** then, because undo also had to update the score. With the journal, undo restores the score for free.

## Tried and rejected

| # | Idea | Result |
|---|---|---|
| A6 | `[SkipLocalsInit]` | No gain (+1.1%). The 6% "zero memory" in the profile came from allocating the 16 MB table per search, outside the timing. |
| — | Narrower dilation, `Prior` through a ref | +0.0%: the JIT already removed the bounds checks. |
| B1 | 4-way 64-byte TT buckets | Suite −0.3% nodes. Matches 49.8% (Elo −1) at 1 thread and 50.5% (+3) at 8 threads. |
| B2 | PVS | −3.4% nodes; match 50.0% (Elo 0). It stays in the code as an option, off by default. Together with LMR it saved only 17% (LMR alone saved 49%). |
| B3 | History tie-break | −13.9% nodes but −6.4% time, because of its own cost. Matches +16 [−9, +41] at 1 thread and +7 [−32, +46] at 8 threads: promising, but not proven. |
| B4 tuning | `lmrMinDepth=2`; `lmrFromMove=2`; deeper reduction for late moves | `lmrMinDepth=2` saved 14.8% nodes, but the match was even (Elo +5). The others saved under 3%. The parameters stay as options. |
| B7 | Lazy SMP: share the deepest completed iteration, odd helpers one ply deeper | No gain in time to depth; 8-thread match 46.7% (Elo −23). Likely cause: Carbon's odd/even depth bias. |
| B8 | VCF (continuous fours) in `QuickWinSearch` | At every node it was 2.8–10× slower. Near the root only (depth ≥ 4) it was +25% time; match 48.8% (Elo −8). |

**Not tried, with the reason.**
- **A3 probe-before-make and B5 ETC:** the counters show table *cutoffs* at only about 6% of interior nodes, because Carbon accepts a stored result only at the same depth parity. Both could save only a few percent.
- **A7 threat masks and A9 cancellation checks:** each targets functions that take under 3% of the time.
- **A10 worker reuse:** a small gain, at very short time limits only.
- **Futility, null move, aspiration windows:** lower priority. Carbon's evaluation swings a lot, and the earlier items left no clear opening.

## Findings worth knowing

- **Lazy SMP scales poorly.** On the suite, 2, 4 and 8 threads reach the same depths only about 1.5× faster than 1 thread, while they search 2–4× as many nodes. In matches 8 threads still help, because the search gets wider. A better parallel search is the largest remaining opportunity, but the first attempt (B7) lost.
- **The docs credited killer and history heuristics that did not exist,** and claimed the port played "100% identical" to Carbon. That had never been tested: the candidate set differed, which B6 has now fixed. The docs are corrected.
- **The per-move table cutoff rule** (same depth parity only) limits what TT-based ideas can gain. It is part of Carbon's evaluation design, so it was left as it is.

## Files

- **Engine:**
  - `src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs`
  - `src/Gomoku.Core/Engine/BitboardPatternBoard.cs`
  - `src/Gomoku.Core/AI/CandSort.cs`
  - `src/Gomoku.Core/AI/CarbonSearchOptions.cs`
  - `src/Gomoku.Core/AI/SearchStats.cs`
  - `src/Gomoku.Core/AI/LockFreeTranspositionTable.cs`
- **Harness:**
  - `tools/Gomoku.Benchmark/{SuiteBenchmark,AbMatch}.cs`
  - `tools/Gomoku.Benchmark/suite-positions.txt`
  - `tools/Gomoku.Benchmark/results/*.json`
- **Tests:** `tests/Gomoku.Core.Tests/EngineSignatureTests.cs` and `TestData/`.
