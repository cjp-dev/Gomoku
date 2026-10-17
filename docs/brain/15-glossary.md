# 15 – Glossary

[Back to the index](README.md)

## In short

This glossary defines standard terminology used across Gomoku and Renju rules, computer game theory, bitboard representation, heuristic evaluation, and the Carbon Gomoku search architecture.

---

## Alphabetical index

[A](#a) · [B](#b) · [C](#c) · [D](#d) · [E](#e) · [F](#f) · [G](#g) · [H](#h) · [I](#i) · [K](#k) · [L](#l) · [M](#m) · [N](#n) · [O](#o) · [P](#p) · [Q](#q) · [R](#r) · [S](#s) · [T](#t) · [U](#u) · [V](#v) · [W](#w) · [Z](#z)

---

### A

#### Adjacency (`Adj1`, `Adj2`)
The geometric stone proximity counters tracked for each board intersection.
- **`Adj1` (1-Ring):** Count of stones in the 8 immediate Moore neighbor cells ($\max(|dx|, |dy|) = 1$).
- **`Adj2` (2-Ring):** Count of stones in the 16 secondary neighbor cells at Chebyshev distance 2 ($\max(|dx|, |dy|) = 2$).
Cells with $\text{Adj1} == 0 \land \text{Adj2} == 0$ are pruned during candidate move generation.

#### Ahead-of-Time Compilation (AOT)
A compilation process where intermediate code (C# IL) is translated into native machine code (WebAssembly bytecode) before deployment, eliminating runtime JIT overhead in the browser.

#### Alpha-Beta Pruning
An adversarial search optimization that prunes subtrees provably worse than previously evaluated alternatives, reducing time complexity from $\mathcal{O}(b^d)$ to $\mathcal{O}(b^{d/2})$ under optimal move ordering.

---

### B

#### Beta Cutoff
In alpha-beta search, an early termination that occurs when a candidate move produces an evaluation strictly greater than the upper bound $\beta$. The maximizing parent player would never allow this branch, so search of remaining siblings is abandoned.

#### Bitboard (8-Cell Ray Bitmask)
An 8-bit unsigned integer representation where each bit indicates the presence or absence of a stone along a specific directional ray originating from an intersection. In Carbon, each cell maintains 4 friendly ray bitmasks and 4 opponent ray bitmasks.

#### Blocked Four (`ThreatD`)
A straight line of four stones with one open end and one blocked end ($\circ \times \times \times \times \cdot$). The opponent is forced to defend by blocking the remaining open end on the immediate reply.

#### Bounding Box (`UpperLeftCand`, `LowerRightCand`)
The dynamic rectangular board sub-region enclosing all placed stones with a 2-cell margin:
$$[x_{\min}-2, x_{\max}+2] \times [y_{\min}-2, y_{\max}+2]$$
Intersections outside this box are never scanned for candidate moves.

#### BoundType
Classification of a score stored in the Transposition Table:
- **`Exact`:** The score fell strictly within $[\alpha, \beta]$.
- **`LowerBound`:** Beta cutoff occurred; the true score is at least this value.
- **`UpperBound`:** Fail low occurred; the true score is at most this value.

---

### C

#### Candidate (`Cand`)
A candidate move struct storing board coordinates $(X, Y)$ and an associated heuristic priority score.

#### Carbon Gomoku
The legendary C++ Gomoku engine authored by Tomasz Czardybon, known for winning the Gomocup international tournament and introducing 4-ray pattern bitboard tensors and closed-interval search.

#### Closed-Interval Negamax
A formulation of the Negamax search algorithm where the search window $[\alpha, \beta]$ is inclusive (closed), and fail-soft returns exceed the boundary by $\pm 1$.

---

### D

#### Dihedral Group $D_4$
The group of 8 planar symmetries of a square, consisting of 4 rotations ($0^\circ, 90^\circ, 180^\circ, 270^\circ$) and 4 reflections (horizontal, vertical, diagonal, anti-diagonal). Used by the opening book to match patterns across all 8 board orientations.

#### Direct Opening
In Renju, an opening sequence where White's second stone is placed directly adjacent to Black's center stone along an orthogonal axis (horizontal or vertical).

#### Double-Four (4x4 Foul)
Under Renju rules, a foul move for Black where placing a single stone creates two or more independent Four formations simultaneously.

#### Double-Three (3x3 Foul / `ThreatF`)
A move that simultaneously creates two open threes. Under Renju rules, this is an illegal foul move for Black; in Freestyle Gomoku, it is a devastating offensive fork.

---

### E

#### Exact5 Variant
A Gomoku rule variant where exactly five stones in a row wins the game, but an overline ($6+$ stones) does not count as a win and the game continues.

#### Evaluation (`Evaluate()`)
The static leaf evaluation function called at search depth $h = 0$, summing the empirical weights of all placed stones across 4 rays using `CarbonTables.Config` and `Rank`.

---

### F

#### Five in a Row (`ThreatA`)
A straight unbroken sequence of 5 stones of the same color along a horizontal, vertical, or diagonal ray. Constitutes an immediate game win.

#### Fork
A single stone placement that simultaneously creates two distinct winning threats along intersecting rays (e.g. Four-Three or Double-Three), overwhelming the opponent's ability to defend both.

#### Four-Three Fork (`ThreatC`)
A move that simultaneously creates an active Four and an Open Three. The defender is forced to block the Four, after which the attacker converts the Open Three into an unstoppable Open Four.

#### Freestyle Variant
The classical rules of Gomoku where five or more stones in a row wins immediately for either player, with zero foul restrictions or asymmetry.

---

### G

#### Gomocup
The prestigious annual international tournament for Gomoku and Renju computer playing engines, founded in 2000.

#### Gomoku Algebraic Record
A human-readable game text format (`.gomoku`) using tagged bracket metadata headers and numbered algebraic move coordinates (`1. H8 H9 2. I9 J10`).

---

### H

#### Half-Open Four
See *Blocked Four*.

#### Hash Move
The best move retrieved from the Transposition Table for the current position, placed at index 0 of the candidate list with score $10,000$ to trigger instant beta cutoffs.

#### Hoshi (Star Point)
The traditional small reference dots placed on Go/Gomoku boards to assist players with coordinate alignment and visual spatial orientation.

---

### I

#### Indirect Opening
In Renju, an opening sequence where White's second stone is placed diagonally adjacent to Black's center stone.

#### Iterative Deepening
A search strategy that executes consecutive minimax searches at increasing depths ($d = 2, 4, 6, 8, \dots$). Ensures optimal move ordering via the Transposition Table and allows clean interruption when time budgets expire.

---

### K

#### Kaya (Hon-Kaya)
The premier, traditional golden-yellow wood harvested from *Torreya nucifera* trees, revered in Japan for Gomoku and Go boards due to its resonant acoustic snap and fine grain.

---

### L

#### LShape
In Carbon's pattern bitboard, the precomputed 2-bit flag array tracking whether an 8-cell ray is truncated by the board boundary within distance 4.

#### LowerBound
A Transposition Table entry representing a beta cutoff score ($\ge \beta$).

---

### M

#### MakeMove / UndoMove
The complementary pair of operations that place a stone on `PatternBoard` and roll it back, incrementally updating ray bitboards, neighbor adjacency counters, and Zobrist hashes in $\mathcal{O}(1)$ time.

#### Moore Neighborhood (`Adj1`)
The 8 cells surrounding a central cell on a square two-dimensional grid.

---

### N

#### Negamax
A mathematical formulation of minimax search based on the zero-sum property:
$$\max(a, b) = -\min(-a, -b)$$
Allows both players to be evaluated using a single recursive maximizing function.

#### Nodes Per Second (NPS)
The standard metric of engine search throughput, measuring how many game tree positions the engine evaluates per second of wall-clock time.

#### `NSt` Tensor
The live $2 \times 10$ integer array maintained incrementally on `PatternBoard`, tracking the exact number of active board threats by player and threat category.

#### Null Window Search (NWS)
An alpha-beta search where the window width is reduced to 1 ($[v_A, v_B] = [-\beta, -(\text{best} + 1)]$) to rapidly test whether a candidate move can beat the current best score without exploring a full minimax tree.

---

### O

#### `ONE_CAND` (Forced Tactical Override)
An engine mechanism that collapses the candidate list to a single forced move when an immediate winning five or forced defense against an opponent four is detected.

#### Open Four (`ThreatB`)
A straight unbroken sequence of four stones with both ends unobstructed ($\cdot \times \times \times \times \cdot$). Because the opponent can only block one end per turn, the other end completes Five on the next turn, making it an unstoppable win in 1 move.

#### Open Three (`ThreatE`)
A straight unbroken sequence of three stones with both ends open ($\cdot \times \times \times \cdot$), threatening to become an Open Four on the next move.

#### Opening Book
A database of precomputed opening lines enabling the engine to execute master-level opening play in sub-millisecond time.

#### Overline
A continuous straight line of six or more stones of the same color. In Freestyle Gomoku, an overline wins; in Exact5, it is ignored; in Renju, it is an illegal foul for Black and a winning line for White.

---

### P

#### `PatternBoard`
The core geometric and bitboard state engine of Gomoku .NET, maintaining cell structures, 4-ray bitmasks, adjacency counters, and threat tensors on a sentinel-padded grid.

#### Principle of Shortest Win & Longest Defense
The game-theoretic imperative that an engine must select the quickest route to victory when winning, and the line of greatest delay and resistance when facing inevitable defeat.

#### `Prior` Matrix
The $256 \times 256$ precomputed lookup table mapping 8-cell friendly and opponent ray bitmasks to heuristic move ordering priority scores.

#### Principal Variation Search (PVS)
An alpha-beta enhancement that searches the first (and presumed best) move with a full window and all alternative moves with a null window.

---

### Q

#### `QuickWinSearch`
The specialized recursive VCF (Victory by Continuous Fours) solver in Carbon Gomoku that discovers tactical checkmates up to 30 plies deep in sub-millisecond time.

---

### R

#### `Rank` Table
The 106-entry empirical weight array mapping canonical ray configurations into static positional scores used by leaf evaluation.

#### Renju Variant
The asymmetrical tournament variant of Gomoku where Black is restricted by foul rules (3x3, 4x4, overlines) to balance the enormous first-mover advantage.

---

### S

#### Sentinel Margin
The 4-cell border of dummy cells encircling the playable board area, preventing ray bitboard loops from requiring boundary bounds-checking instructions.

#### Smart Game Format (SGF)
The international text-based standard format for board game records (Gomoku identifier `GM[11]`).

#### `STATUS1`
The $256 \times 256$ lookup table mapping 8-cell ray bitmasks into a 4-bit threat grade along a single ray.

#### `STATUS4`
The 10,000-cell tensor combining 4 single-ray `STATUS1` grades into a comprehensive composite tactical classification (`ThreatA` through `ThreatForbid`).

---

### T

#### Transposition Table (TT)
A high-speed hash table caching search depths, bounds, evaluations, and best moves to eliminate redundant search across transposed move orders.

---

### V

#### VCF (Victory by Continuous Fours)
A sequence of moves where the attacking player creates an active Four on every single turn, forcing the defender to block on each immediate reply until Five is achieved.

#### VCT (Victory by Continuous Threes)
A sequence of moves where the attacker maintains initiative by creating continuous Three and Four threats, culminating in an unstoppable Open Four.

---

### W

#### `WinMax` / `WinMin`
The evaluation constants ($30,000$ and $25,000$) denoting proven tactical checkmates. Scores decaying between $\text{WinMin}$ and $\text{WinMax}$ reflect the ply distance to mate.

---

### Z

#### Zobrist Hashing
A technique that assigns 64-bit pseudo-random values to each stone and turn state, computing the composite board hash via XOR ($\oplus$) in $\mathcal{O}(1)$ time.

---

Previous: [14 Benchmarks and performance](14-benchmarks-and-performance.md) · Next: [16 References](16-references.md)

