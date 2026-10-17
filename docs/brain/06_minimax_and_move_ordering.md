# Chapter 6: Minimax Search & Move Ordering

## 6.1 The Gomoku Branching Problem
On a standard $15 \times 15$ Gomoku board with 225 intersections, unpruned minimax search exhibits an astronomical branching factor. Searching 6 plies deep would require evaluating over $200^6 \approx 64 \text{ trillion}$ leaf states.

Carbon AI solves this problem by combining:
1. **Aggressive Move Pruning (`GenerateCand` & `ONE_CAND` Tactical Overrides)**
2. **Closed-Interval $[\alpha, \beta]$ Negamax Search with Mate-Distance Adjusting**
3. **Iterative Deepening with Predictive Time Management (`TimeoutPrevent`)**
4. **Depth-Parity & Mate-Aware Zobrist Transposition Table Cutoffs**

![Closed-Interval Alpha-Beta & VCF Search Pipeline](images/pv-search-tree.svg)

```mermaid
graph TD
    Root[Current Board Position] --> BoundingBox[Incremental 2-Ring Bounding Box]
    BoundingBox --> FilterEmpty[Filter Empty Cells with Adj1/Adj2 > 0]
    FilterEmpty --> ThreatGrade[Score Priority via Prior[256][256] + Adj1]
    ThreatGrade --> TacticalOverride{Forced Tactical Threat?}
    TacticalOverride -- ThreatA / Own ThreatB --> OneCand[ONE_CAND: Prune to 1 Forced Move]
    TacticalOverride -- Opp ThreatB --> RestrictE[Filter to Moves with Status4 >= E]
    TacticalOverride -- Normal --> Sort[Quicksort Descending by Priority]
    OneCand --> AlphaBeta[Closed-Interval Alpha-Beta Search]
    RestrictE --> Sort
    Sort --> AlphaBeta
```

---

## 6.2 Candidate Generation (`GenerateCand`)
Rather than scanning the entire board, Carbon tracks candidate squares incrementally:
1. **Transposition Best Move First:** If the current Zobrist key hits in the Transposition Table and has a stored `BestMove`, it is placed at `cnd[0]` with priority `10000`.
2. **Bounding Box & Adjacency Rings:** Maintains `UpperLeftCand` and `LowerRightCand` expanded by 2 cells around all placed stones. Only empty squares with `Adj1 != 0` (Chebyshev distance 1) or `Adj2 != 0` (ray-aligned distance 2) are considered.
3. **Priority Scoring (`Cell.Prior()`):**
   $$\text{Prior}(x, y) = \sum_{k=0}^{3} \left(\text{PRIOR}[\text{pat}_{k,0}][\text{pat}_{k,1}] + \text{PRIOR}[\text{pat}_{k,1}][\text{pat}_{k,0}]\right) + [\text{Adj1} \neq 0]$$
   Only candidates with $\text{Prior}(x, y) > 1$ are retained (up to `MaxCand = 256`).
4. **Forced Tactical Overrides (`ONE_CAND`):**
   - If the side to move has a `ThreatA` (five-in-a-row), `ForceOneCand` prunes the candidate list to $1$ winning move.
   - Else if the opponent has a `ThreatA` (four threat), `ForceOneCand` prunes the list to the $1$ mandatory blocking move.
   - Else if the side to move has a `ThreatB` (open four or double-four), `ForceOneCand` prunes the list to $1$ unstoppable move.
   - Else if the opponent has a `ThreatB` (open three threat), candidate generation restricts moves solely to squares with `Status4 >= ThreatE` (blocking the open three or counter-attacking with a four).

---

## 6.3 Closed-Interval $[\alpha, \beta]$ Search & Mate Distance Scaling
Following `AICarbon::minimax`, the search operates on a **closed** interval $[\alpha, \beta]$:
- `best.Value` is initialized to $\alpha - 1$ (fail-low sentinel).
- For each candidate move, the child search is invoked with narrowed bounds:
  $$v_A = -\beta, \qquad v_B = -(\text{best.Value} + 1)$$
- To prefer faster wins in $k$ plies over $k+1$ plies, win scores ($\ge \text{WinMin} = 25000$) are stretched on entry (`if (vB >= WinMin) vB++; if (vA <= -WinMin) vA--;`) and decayed by 1 per ply on return (`if (value >= WinMin) value--; if (value <= -WinMin) value++;`).
- A beta cutoff occurs strictly when `value > beta`, returning `beta + 1`.
- Transposition Table hits at non-root nodes are accepted only when `(entry.Depth >= h && ((entry.Depth ^ h) & 1) == 0) || Math.Abs(entry.Score) >= WinMin`, preserving odd/even ply tempo parity.

