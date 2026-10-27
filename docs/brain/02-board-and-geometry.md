# 02 – Board and geometry

[Back to the index](README.md)

## In short

Gomoku is played on a grid of intersections using black and white stones. Unlike Chess or Checkers, stones never move or leave the board once placed. The core engine mathematically supports arbitrary rectangular board dimensions up to $64 \times 32$ (with support for Go $19 \times 19$ and Gomocup $20 \times 20$ test geometries in domain models). However, the Gomoku .NET application has standardized completely on the official international **$15 \times 15$ Standard** tournament geometry for gameplay and UI. To guarantee maximum evaluation throughput with zero branch mispredictions, the internal `PatternBoard` wraps the active playable grid in a 4-cell sentinel border of `PieceColor.Wrong` cells, eliminating all bounds-checking loops during 4-axis ray updates.

---

## Board dimensions & coordinate systems

### Standard Geometries

| Geometry | Standard Tournament Context | Coordinate Range | Total Intersections | Center Point | Star Points ($N$) |
|---|---|---|---|---|---|
| **$15 \times 15$** | Renju International Federation (RIF) & modern tournament Gomoku | `A1` .. `O15` | 225 | `H8` $(7, 7)$ | 5 points: `D4`, `L4`, `H8`, `D12`, `L12` |
| **$19 \times 19$** | Traditional Go board & original Carbon Gomoku default | `A1` .. `S19` | 361 | `J10` $(9, 9)$ | 9 points: 4 corners, 4 edges, 1 center |
| **$20 \times 20$** | International Gomocup Computer Tournament standard | `A1` .. `T20` | 400 | `K11` $(10, 10)$ | 4 points: symmetric quadrant markers |

### Coordinate Representation

Coordinates are represented algebraically using consecutive column letters `A` through `T` (left to right) and row numbers `1` through `N` (top to bottom):
- Note that unlike the game of Go (which omits the letter `I` to avoid confusion with `J` or `1`), Gomoku standards strictly retain `I`.
- On a $15 \times 15$ board, the top-left square is `A1` $(0, 0)$, the geometric center is `H8` $(7, 7)$, and the bottom-right square is `O15` $(14, 14)$.
- Coordinates are encapsulated by the immutable [`Point`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/Point.cs) struct:
  ```csharp
  public readonly record struct Point(int X, int Y)
  {
      public string ToCoordinateString() => $"{(char)('A' + X)}{Y + 1}";
      public bool IsValid(int width, int height) => X >= 0 && X < width && Y >= 0 && Y < height;
  }
  ```

---

## Sentinel padding & memory layout

![Sentinel-Padded PatternBoard Geometry](images/board-indexing.svg)

### Why 4-Cell Margin Padding?

Every stone placement in Gomoku propagates tactical threats along 4 ray axes (horizontal, vertical, main diagonal, anti-diagonal) up to 4 squares away in both positive and negative directions:
$$\Delta \in \{-4, -3, -2, -1, +1, +2, +3, +4\} \times (DX_k, DY_k)$$

If the board were stored as a raw $W \times H$ array, every single step along each of the 4 rays would require conditional boundary checks:
```csharp
// SLOW: 4 ray loops x 8 steps x 2 bounds checks = 64 branches per move!
if (x >= 0 && x < width && y >= 0 && y < height) { ... }
```

By adding a margin of **4 sentinel cells** on all four sides:
$\text{Allocated Width} = W + 8, \qquad \text{Allocated Height} = H + 8$
$\text{Playable Range} = [4 \dots W + 3, \quad 4 \dots H + 3]$

Any ray starting at an edge cell (such as $(4, 4)$) and stepping 4 squares in the negative direction lands at $(0, 0)$, which is safely inside the allocated memory buffer. **Bounds checking is completely eliminated from the inner search loops.**

### 1D Memory Layout & Stride Indexing (`BitboardPatternBoard`)
While historical `PatternBoard` used a 2D array `Cell[x, y]`, the production `BitboardPatternBoard` stores cells in a flat 1D unmanaged array using a power-of-two row stride:
$\text{Stride} = 128, \qquad \text{Index}(x, y) = (y \ll 7) \mid x$

Because $128 = 2^7$, cell offset calculation compiles down to a single bit-shift and OR instruction (`lea / shl / or`), with zero multiplication latency and optimal cache-line alignment.

### The `PieceColor.Wrong` Sentinel

The sentinel cells ($x < 4$ or $y < 4$ or $x \ge W+4$ or $y \ge H+4$) are initialized with `PieceColor.Wrong`. In Carbon Gomoku's pattern encoding:
- `PieceColor.Empty` $(2)$ represents an unoccupied intersection.
- `PieceColor.Black` $(0)$ and `PieceColor.White` $(1)$ represent played stones.
- `PieceColor.Wrong` $(3)$ represents an impassable border.

During `PatternBoard.Start()`, sentinel cells pre-populate the boundary bits of all adjacent playable cells with blocking bits (`0x0F` and `0xF0`). Consequently, stones placed near the edge of the board automatically recognize the board edge as an impenetrable block, exactly identical to an opponent stone.

---

## Data structures

### `BoardState` (Core Domain Model)
`BoardState` is the lightweight model representing an active game position:
```csharp
public sealed class BoardState
{
    private readonly PieceColor[] _cells; // 1D array: length = Width * Height
    public BoardSize Size { get; }
    public GomokuVariant Variant { get; }
    public PieceColor SideToMove { get; internal set; }
    public PieceColor FirstPlayer { get; }
    public int MoveCount { get; internal set; }
    public ulong Hash { get; internal set; }
    public GameStatus Status { get; internal set; }
    public IReadOnlyList<Point> WinningLine { get; internal set; }
}
```

### `BitboardPatternBoard.Cell` (Production Engine Search Node)
In the active production engine (`BitboardPatternBoard`), each intersection is stored as an unmanaged 19-byte struct (1 piece byte, 8 pattern bytes, 8 `Status1` bytes, 2 `Status4` bytes):
```csharp
public struct Cell
{
    public PieceColor Piece;          // Black (0), White (1), Empty (2), or Wrong (3); 1 byte
    public byte Pattern00, Pattern01; // Ray 0 (horizontal) bitmasks for Black/White
    public byte Pattern10, Pattern11; // Ray 1 (vertical) bitmasks for Black/White
    public byte Pattern20, Pattern21; // Ray 2 (main diagonal) bitmasks for Black/White
    public byte Pattern30, Pattern31; // Ray 3 (anti-diagonal) bitmasks for Black/White

    public byte Status1_00, Status1_01; // Directional threat grades 0..9 (Ray 0)
    public byte Status1_10, Status1_11; // Directional threat grades 0..9 (Ray 1)
    public byte Status1_20, Status1_21; // Directional threat grades 0..9 (Ray 2)
    public byte Status1_30, Status1_31; // Directional threat grades 0..9 (Ray 3)

    public byte Status4_0, Status4_1;   // Combined 4D threat index (0..9) for Black/White
}
```

> [!NOTE] Architectural Evolution: Historical `PatternBoard.Cell` and Adjacency Counters
> In the baseline `PatternBoard.cs` (direct port of Michał Czardybon's 2001 Carbon Gomoku), the `Cell` struct additionally contained two signed bytes:
> ```csharp
> public sbyte Adj1, Adj2; // 2-ring candidate proximity counters
> ```
> In `BitboardPatternBoard`, these fields were removed entirely. Move candidates are instead computed when they are generated, with bitwise shifts on the `Occupied` row bitboards, eliminating 24 memory writes per move (measured at the time as a +70% search speedup). Since the `CarbonCandidates` option (default on), the shifts produce exactly Carbon's `Adj1`/`Adj2` sets (see below).

---

## Candidate bounding box & bitboard candidate masks

In tournament Gomoku, scanning all 225 intersections at every search node is computationally prohibitive. On move 2, evaluating empty squares across the opposite corner of the board produces an intolerable branching factor without offering any tactical value.

`BitboardPatternBoard` accelerates candidate generation using two complementary geometric mechanisms:

### 1. The Dynamic Bounding Box
The engine tracks the minimal rectangular bounding box enclosing all placed stones, expanded by a margin of 2 cells in every direction:
$\text{UpperLeftCand} = \big(\max(4, \min_i(x_i - 2)), \ \max(4, \min_i(y_i - 2))\big)$
$\text{LowerRightCand} = \big(\min(W + 3, \max_i(x_i + 2)), \ \min(H + 3, \max_i(y_i + 2))\big)$

- On move 1, the bounding box spans only a $5 \times 5$ window ($25$ cells).
- As stones are played, the box expands dynamically.
- Empty cells outside this bounding box are pruned before tactical evaluation begins.

### 2. Bitboard Candidate Masks (`GenerateCarbonCand`, `DilateRow` & `GetCandidateMask`)
Within the bounding box, an empty square is only tactically relevant if a stone is near it. With `CarbonSearchOptions.CarbonCandidates` (the default), the engine uses Carbon's own neighbourhood: a stone in the $3 \times 3$ ring around the cell (Carbon's `adj1`), or a stone two steps away along one of the four lines (`adj2`):
$$\text{adj1}: \max(|dx|, |dy|) = 1, \qquad \text{adj2}: (dx, dy) \in \{-2, 0, 2\}^2 \setminus \{(0, 0)\}$$

With `CarbonCandidates` off, the older full $5 \times 5$ square is used instead, i.e. every empty cell within Chebyshev distance $R \le 2$ of a stone:
$D_{\text{Chebyshev}}\big((x_1, y_1), (x_2, y_2)\big) = \max(|x_1 - x_2|, |y_1 - y_2|) \le 2$

Rather than maintaining per-cell neighbor counters in memory, `BitboardPatternBoard` maintains an array of 32-bit row bitboards (`uint[] Occupied`). Stone placement and retraction update the occupied bitboard in a single CPU cycle:
```csharp
// 1. MakeMove: Update occupied bit in 1 single CPU cycle (0 memory writes for neighbors)
Occupied[by] |= (1u << bx);

// 2. UndoMove: Clear occupied bit in 1 single CPU cycle
Occupied[by] ^= (1u << bx);
```

During candidate generation in `CarbonBitboardAiPlayer`, Carbon's set is built per row `by` from the occupied rows $by-2 \dots by+2$:
```csharp
uint near = up | row | down;                                   // rows by-1, by, by+1
uint ring = (near | (near << 1) | (near >> 1)) & boardMask;     // adj1: the 3x3 ring
uint far2 = up2 | down2;                                       // rows by-2, by+2
uint two = ((row << 2) | (row >> 2) | far2 | (far2 << 2) | (far2 >> 2)) & boardMask; // adj2
uint mask = (ring | two) & ~row & boardMask;                   // empty candidate cells
```

The older $5 \times 5$ path (`CarbonCandidates = 0`) dilates every row twice and ORs five rows:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static uint DilateRow(uint m, uint boardMask)
{
    // Step 1: Dilate by 1 square (left, center, right)
    uint d1 = (m | (m << 1) | (m >> 1)) & boardMask;
    // Step 2: Dilate by 2 squares (left 2, right 2)
    return (d1 | (d1 << 1) | (d1 >> 1)) & boardMask;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public uint GetCandidateMask(int by, ReadOnlySpan<uint> dilatedRows, uint boardMask)
{
    uint mask = 0;
    int y0 = Math.Max(0, by - 2);
    int y1 = Math.Min(BoardHeight - 1, by + 2);
    for (int r = y0; r <= y1; r++)
    {
        mask |= dilatedRows[r];
    }
    // Mask out cells already occupied by stones:
    return mask & ~Occupied[by] & boardMask;
}
```
Candidate extraction loops over the non-zero bits using hardware-accelerated trailing-zero-count instructions (`BitOperations.TrailingZeroCount`), achieving zero branch mispredictions on non-candidate cells.

> [!NOTE] Architectural Evolution: The Legacy 2-Ring Proximity Counters (`Adj1` & `Adj2`)
> In the original `Carbon-Gomoku-master` (Michał Czardybon, 2001) and baseline `PatternBoard.cs`, candidate filtering was tracked by maintaining two concentric counters per cell:
> - `Adj1` (8-Neighbor Ring): Count of stones in the 8 Chebyshev distance-1 neighbors:
>   $\{(x \pm 1, y), (x, y \pm 1), (x \pm 1, y \pm 1)\}$
> - `Adj2` (Ray-Aligned Distance-2 Ring): Count of stones in the 8 ray-aligned distance-2 neighbors:
>   $\{(x \pm 2, y), (x, y \pm 2), (x \pm 2, y \pm 2)\}$
>
> On every make and undo operation, Carbon had to iterate and increment/decrement 24 individual cell counters:
> ```csharp
> Cells[xp - 1, yp - 1].Adj1++; Cells[xp, yp - 1].Adj1++; Cells[xp + 1, yp - 1].Adj1++;
> Cells[xp - 1, yp    ].Adj1++;                           Cells[xp + 1, yp    ].Adj1++;
> Cells[xp - 1, yp + 1].Adj1++; Cells[xp, yp + 1].Adj1++; Cells[xp + 1, yp + 1].Adj1++;
>
> Cells[xp - 2, yp - 2].Adj2++; Cells[xp, yp - 2].Adj2++; Cells[xp + 2, yp - 2].Adj2++;
> Cells[xp - 2, yp    ].Adj2++;                           Cells[xp + 2, yp    ].Adj2++;
> Cells[xp - 2, yp + 2].Adj2++; Cells[xp, yp + 2].Adj2++; Cells[xp + 2, yp + 2].Adj2++;
> ```
> During candidate generation, cells were filtered with `c.Adj1 > 0 || c.Adj2 > 0`. Replacing these 24 array element writes per node with whole-board bitwise dilation increased search throughput by **+70%** and make/undo speed by **$2.5\times$** (historical measurement). The first bitboard version widened the set to the full $5 \times 5$ square; `CarbonCandidates` (default on) restores Carbon's exact `Adj1`/`Adj2` set computed from `Occupied`, which searched $14\%$ fewer nodes and won a 730-game match (1 thread, $0.2\text{ s}$ per move) by $+33$ Elo.

---

## Design notes

- **Fixed-Capacity Preallocation:** `BitboardPatternBoard.Cells` is allocated once as `Cell[Stride * MaxHeightPadded]` ($128 \times 40 = 5,120$ elements). Calling `Start(width, height)` resets the active dimensions without heap allocation, eliminating garbage collection pauses during search.
- **Fast Coordinate Conversion:** To convert between padded engine coordinates $(x_p, y_p) \in [4 \dots W+3, \ 4 \dots H+3]$ and 0-indexed board coordinates $(x, y) \in [0 \dots W-1, \ 0 \dots H-1]$:
  $$x = x_p - 4, \qquad y = y_p - 4$$
- **Stack-Allocated History:** History undo stacks (`_remMove`, `_remULCand`, `_remLRCand`, `RemPlacedPoints`) are pre-allocated flat arrays capable of storing full games up to $64 \times 32 = 2,048$ plies.
- **Undo Journal:** Before each move, `MakePaddedMove` copies the 33 cells the move can change (the played cell and $4 \times 8$ ray neighbours) plus `NSt` and `StoneScore` into a journal slot; `UndoMove` copies them back. The journal starts with 64 slots and doubles when more moves than that have been made since the journal base (see [04](04-pattern-bitboards-and-lookup-tables.md)).

---

## Where in the code

| File | Member / Type | Description |
|---|---|---|
| [BoardSize.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/BoardSize.cs) | `BoardSize` | Board dimensions, validation, and star point calculations |
| [Point.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/Point.cs) | `Point` | 2D coordinate struct and algebraic formatting (`A1` .. `T20`) |
| [PieceColor.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/PieceColor.cs) | `PieceColor` | Cell states (`Black = 0`, `White = 1`, `Empty = 2`, `Wrong = 3`) |
| [BoardState.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/BoardState.cs) | `BoardState` | Immutable game state snapshot and 1D cell indexing |
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `BitboardPatternBoard` | High-performance 1D pattern board with occupied row bitboards and undo journal |
| [CarbonBitboardAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) | `GenerateCarbonCand` | Carbon's `adj1`/`adj2` candidate set from row bitmask shifts |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `PatternBoard`, `Cell` | Sentinel-padded 2D bitboard array, bounding box, and rings |

---

## Tests

- [BoardAndRulesTests.cs](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/BoardAndRulesTests.cs):
  - Validates coordinate string conversions (`A1`, `H8`, `O15`, `S19`).
  - Verifies star points for $15 \times 15$ (5 points), $19 \times 19$ (9 points), and $20 \times 20$ (4 points).
  - Confirms out-of-bounds coordinate rejection.
- [AiTests.cs](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests/AiTests.cs):
  - Tests first-move center opening generation across different board sizes.
  - Confirms bounding box expansion and contraction across make/undo sequences.

---

Previous: [01 Overview](01-overview.md) · Next: [03 Rules and variants](03-rules-and-variants.md)

