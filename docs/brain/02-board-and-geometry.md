# 02 – Board and geometry

[Back to the index](README.md)

## In short

Gomoku is played on a grid of intersections using black and white stones. Unlike Chess or Checkers, stones never move or leave the board once placed. The engine supports arbitrary rectangular board dimensions up to $64 \times 32$, with first-class presets for $15 \times 15$ (modern international tournament standard), $19 \times 19$ (classical Go board and Carbon Gomoku default), and $20 \times 20$ (international Gomocup AI competition standard). To guarantee maximum evaluation throughput with zero branch mispredictions, the internal `PatternBoard` wraps the active playable grid in a 4-cell sentinel border of `PieceColor.Wrong` cells, eliminating all bounds-checking loops during 4-axis ray updates.

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
- Coordinates are encapsulated by the immutable [`Point`](../../src/Gomoku.Core/Models/Point.cs) struct:
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
$$\text{Allocated Width} = W + 8, \qquad \text{Allocated Height} = H + 8$$
$$\text{Playable Range} = [4 \dots W + 3, \quad 4 \dots H + 3]$$

Any ray starting at an edge cell (such as $(4, 4)$) and stepping 4 squares in the negative direction lands at $(0, 0)$, which is safely inside the allocated memory buffer. **Bounds checking is completely eliminated from the inner search loops.**

### The `PieceColor.Wrong` Sentinel

The sentinel cells ($x < 4$ or $y < 4$ or $x \ge W+4$ or $y \ge H+4$) are initialized with `PieceColor.Wrong`. In Carbon Gomoku's pattern encoding:
- `PieceColor.Empty` $(0)$ represents an unoccupied intersection.
- `PieceColor.Black` $(0)$ and `PieceColor.White` $(1)$ represent played stones.
- `PieceColor.Wrong` $(2)$ represents an impassable border.

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

### `PatternBoard.Cell` (Engine Search Node)
In `PatternBoard`, each intersection is stored as an unmanaged 16-byte struct:
```csharp
public struct Cell
{
    public PieceColor Piece;       // Empty, Black, White, or Wrong
    public byte Pattern00, Pattern01; // Ray 0 (horizontal) bitmasks for Black/White
    public byte Pattern10, Pattern11; // Ray 1 (vertical) bitmasks for Black/White
    public byte Pattern20, Pattern21; // Ray 2 (main diagonal) bitmasks for Black/White
    public byte Pattern30, Pattern31; // Ray 3 (anti-diagonal) bitmasks for Black/White

    public byte Status1_00, Status1_01; // Directional threat grades 0..9 (Ray 0)
    public byte Status1_10, Status1_11; // Directional threat grades 0..9 (Ray 1)
    public byte Status1_20, Status1_21; // Directional threat grades 0..9 (Ray 2)
    public byte Status1_30, Status1_31; // Directional threat grades 0..9 (Ray 3)

    public byte Status4_0, Status4_1;   // Combined 4D threat index (0..9) for Black/White
    public sbyte Adj1, Adj2;            // 2-ring candidate proximity counters
}
```

---

## Candidate bounding box & 2-ring adjacency

In the opening and midgame, scanning the entire board for candidate moves is highly wasteful: on a $19 \times 19$ board, evaluating all 361 intersections at every search node creates an intolerable branching factor.

`PatternBoard` maintains two geometric acceleration structures:

### 1. The Dynamic Bounding Box
`PatternBoard` tracks the bounding rectangle enclosing all placed stones, expanded by a margin of 2 cells:
$$\text{UpperLeftCand} = \big(\max(4, \min_i(x_i - 2)), \ \max(4, \min_i(y_i - 2))\big)$$
$$\text{LowerRightCand} = \big(\min(W + 3, \max_i(x_i + 2)), \ \min(H + 3, \max_i(y_i + 2))\big)$$

On move 1, the bounding box spans only a $5 \times 5$ window ($25$ cells). As stones are placed, the box expands incrementally. Unoccupied cells outside this bounding box are never evaluated.

### 2. The 2-Ring Proximity Counters (`Adj1` & `Adj2`)
Within the bounding box, a move is only strategically sensible if it is adjacent to existing stones:
- **`Adj1` (8-Neighbor Ring):** Incremented for the 8 Chebyshev distance-1 neighbors:
  $$\{(x \pm 1, y), (x, y \pm 1), (x \pm 1, y \pm 1)\}$$
- **`Adj2` (Ray-Aligned Distance-2 Ring):** Incremented for the 8 ray-aligned distance-2 neighbors:
  $$\{(x \pm 2, y), (x, y \pm 2), (x \pm 2, y \pm 2)\}$$

```csharp
// When stone (xp, yp) is played:
Cells[xp - 1, yp - 1].Adj1++; Cells[xp, yp - 1].Adj1++; Cells[xp + 1, yp - 1].Adj1++;
Cells[xp - 1, yp    ].Adj1++;                           Cells[xp + 1, yp    ].Adj1++;
Cells[xp - 1, yp + 1].Adj1++; Cells[xp, yp + 1].Adj1++; Cells[xp + 1, yp + 1].Adj1++;

Cells[xp - 2, yp - 2].Adj2++; Cells[xp, yp - 2].Adj2++; Cells[xp + 2, yp - 2].Adj2++;
Cells[xp - 2, yp    ].Adj2++;                           Cells[xp + 2, yp    ].Adj2++;
Cells[xp - 2, yp + 2].Adj2++; Cells[xp, yp + 2].Adj2++; Cells[xp + 2, yp + 2].Adj2++;
```

During candidate generation, an empty square $(x, y)$ inside the bounding box is only considered if:
$$\text{Cell}[x, y].\text{Adj1} > 0 \quad \lor \quad \text{Cell}[x, y].\text{Adj2} > 0$$

Isolated squares with no nearby stones are pruned before tactical evaluation begins.

---

## Design notes

- **Fixed-Capacity Preallocation:** `PatternBoard.Cells` is allocated once as `Cell[MaxWidth + 8, MaxHeight + 8]` ($72 \times 40 = 2,880$ elements). Calling `Start(width, height)` resets the active dimensions without heap allocation, eliminating garbage collection during match play.
- **Fast Coordinate Conversion:** To convert between padded engine coordinates $(x_p, y_p) \in [4 \dots W+3, \ 4 \dots H+3]$ and 0-indexed board coordinates $(x, y) \in [0 \dots W-1, \ 0 \dots H-1]$:
  $$x = x_p - 4, \qquad y = y_p - 4$$
- **Stack-Allocated History:** History undo stacks (`_remMove`, `_remULCand`, `_remLRCand`, `RemPlacedPoints`) are pre-allocated flat arrays capable of storing full games up to $64 \times 32 = 2,048$ plies.

---

## Where in the code

| File | Member / Type | Description |
|---|---|---|
| [BoardSize.cs](../../src/Gomoku.Core/Models/BoardSize.cs) | `BoardSize` | Board dimensions, validation, and star point calculations |
| [Point.cs](../../src/Gomoku.Core/Models/Point.cs) | `Point` | 2D coordinate struct and algebraic formatting (`A1` .. `T20`) |
| [PieceColor.cs](../../src/Gomoku.Core/Models/PieceColor.cs) | `PieceColor` | Cell states (`Empty = 0`, `Black = 0`, `White = 1`, `Wrong = 2`) |
| [BoardState.cs](../../src/Gomoku.Core/Models/BoardState.cs) | `BoardState` | Immutable game state snapshot and 1D cell indexing |
| [PatternBoard.cs](../../src/Gomoku.Core/Engine/PatternBoard.cs) | `PatternBoard`, `Cell` | Sentinel-padded 2D bitboard array, bounding box, and rings |

---

## Tests

- [BoardTests.cs](../../tests/Gomoku.Core.Tests/BoardTests.cs):
  - Validates coordinate string conversions (`A1`, `H8`, `O15`, `S19`).
  - Verifies star points for $15 \times 15$ (5 points), $19 \times 19$ (9 points), and $20 \times 20$ (4 points).
  - Confirms out-of-bounds coordinate rejection.
- [AiTests.cs](../../tests/Gomoku.Core.Tests/AiTests.cs):
  - Tests first-move center opening generation across different board sizes.
  - Confirms bounding box expansion and contraction across make/undo sequences.

---

Previous: [01 Overview](01-overview.md) · Next: [03 Rules and variants](03-rules-and-variants.md)

