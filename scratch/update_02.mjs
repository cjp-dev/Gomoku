import fs from 'fs';

const filePath = 'docs/brain/02-board-and-geometry.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Update Sentinel padding & memory layout
const oldSentSection = `By adding a margin of **4 sentinel cells** on all four sides:
$$\\text{Allocated Width} = W + 8, \\qquad \\text{Allocated Height} = H + 8$$
$$\\text{Playable Range} = [4 \\dots W + 3, \\quad 4 \\dots H + 3]$$

Any ray starting at an edge cell (such as $(4, 4)$) and stepping 4 squares in the negative direction lands at $(0, 0)$, which is safely inside the allocated memory buffer. **Bounds checking is completely eliminated from the inner search loops.**`;

const newSentSection = `By adding a margin of **4 sentinel cells** on all four sides:
$$\\text{Allocated Width} = W + 8, \\qquad \\text{Allocated Height} = H + 8$$
$$\\text{Playable Range} = [4 \\dots W + 3, \\quad 4 \\dots H + 3]$$

Any ray starting at an edge cell (such as $(4, 4)$) and stepping 4 squares in the negative direction lands at $(0, 0)$, which is safely inside the allocated memory buffer. **Bounds checking is completely eliminated from the inner search loops.**

### 1D Memory Layout & Stride Indexing (\`BitboardPatternBoard\`)
While historical \`PatternBoard\` used a 2D array \`Cell[x, y]\`, the production \`BitboardPatternBoard\` stores cells in a flat 1D unmanaged array using a power-of-two row stride:
$$\\text{Stride} = 128, \\qquad \\text{Index}(x, y) = (y \\ll 7) \\mid x$$

Because $128 = 2^7$, cell offset calculation compiles down to a single bit-shift and OR instruction (\`lea / shl / or\`), with zero multiplication latency and optimal cache-line alignment.`;

if (!content.includes(oldSentSection)) {
  console.log('WARNING: oldSentSection not found');
} else {
  content = content.replace(oldSentSection, newSentSection);
}

// 2. Replace PatternBoard.Cell with BitboardPatternBoard.Cell
const oldCellStruct = `### \`PatternBoard.Cell\` (Engine Search Node)
In \`PatternBoard\`, each intersection is stored as an unmanaged 16-byte struct:
\`\`\`csharp
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
\`\`\``;

const newCellStruct = `### \`BitboardPatternBoard.Cell\` (Production Engine Search Node)
In the active production engine (\`BitboardPatternBoard\`), each intersection is stored as an unmanaged 16-byte struct:
\`\`\`csharp
public struct Cell
{
    public PieceColor Piece;          // Empty (0), Black (0), White (1), or Wrong (2)
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
\`\`\`

> [!NOTE] Architectural Evolution: Historical \`PatternBoard.Cell\` and Adjacency Counters
> In the baseline \`PatternBoard.cs\` (direct port of Michał Czardybon's 2001 Carbon Gomoku), the \`Cell\` struct additionally contained two signed bytes:
> \`\`\`csharp
> public sbyte Adj1, Adj2; // 2-ring candidate proximity counters
> \`\`\`
> In \`BitboardPatternBoard\`, these fields were removed entirely. Move candidate detection is instead evaluated dynamically using whole-board bitwise Chebyshev dilation on the \`Occupied\` bitboards, eliminating 24 memory writes per move and achieving a +70% search speedup.`;

if (!content.includes(oldCellStruct)) {
  console.log('WARNING: oldCellStruct not found');
} else {
  content = content.replace(oldCellStruct, newCellStruct);
}

// 3. Candidate bounding box & Chebyshev dilation
const oldCandidateSection = `## Candidate bounding box & 2-ring adjacency

In the opening and midgame, scanning the entire board for candidate moves is highly wasteful: on a $19 \\times 19$ board, evaluating all 361 intersections at every search node creates an intolerable branching factor.

\`PatternBoard\` maintains two geometric acceleration structures:

### 1. The Dynamic Bounding Box
\`PatternBoard\` tracks the bounding rectangle enclosing all placed stones, expanded by a margin of 2 cells:
$$\\text{UpperLeftCand} = \\big(\\max(4, \\min_i(x_i - 2)), \\ \\max(4, \\min_i(y_i - 2))\\big)$$
$$\\text{LowerRightCand} = \\big(\\min(W + 3, \\max_i(x_i + 2)), \\ \\min(H + 3, \\max_i(y_i + 2))\\big)$$

On move 1, the bounding box spans only a $5 \\times 5$ window ($25$ cells). As stones are placed, the box expands incrementally. Unoccupied cells outside this bounding box are never evaluated.

### 2. The 2-Ring Proximity Counters (\`Adj1\` & \`Adj2\`)
Within the bounding box, a move is only strategically sensible if it is adjacent to existing stones:
- **\`Adj1\` (8-Neighbor Ring):** Incremented for the 8 Chebyshev distance-1 neighbors:
  $$\\{(x \\pm 1, y), (x, y \\pm 1), (x \\pm 1, y \\pm 1)\\}$$
- **\`Adj2\` (Ray-Aligned Distance-2 Ring):** Incremented for the 8 ray-aligned distance-2 neighbors:
  $$\\{(x \\pm 2, y), (x, y \\pm 2), (x \\pm 2, y \\pm 2)\\}$$

\`\`\`csharp
// When stone (xp, yp) is played:
Cells[xp - 1, yp - 1].Adj1++; Cells[xp, yp - 1].Adj1++; Cells[xp + 1, yp - 1].Adj1++;
Cells[xp - 1, yp    ].Adj1++;                           Cells[xp + 1, yp    ].Adj1++;
Cells[xp - 1, yp + 1].Adj1++; Cells[xp, yp + 1].Adj1++; Cells[xp + 1, yp + 1].Adj1++;

Cells[xp - 2, yp - 2].Adj2++; Cells[xp, yp - 2].Adj2++; Cells[xp + 2, yp - 2].Adj2++;
Cells[xp - 2, yp    ].Adj2++;                           Cells[xp + 2, yp    ].Adj2++;
Cells[xp - 2, yp + 2].Adj2++; Cells[xp, yp + 2].Adj2++; Cells[xp + 2, yp + 2].Adj2++;
\`\`\`

During candidate generation, an empty square $(x, y)$ inside the bounding box is only considered if:
$$\\text{Cell}[x, y].\\text{Adj1} > 0 \\quad \\lor \\quad \\text{Cell}[x, y].\\text{Adj2} > 0$$

Isolated squares with no nearby stones are pruned before tactical evaluation begins.

### 3. Bitboard Chebyshev Dilation Acceleration (\`BitboardPatternBoard\`)
In the primary production engine (\`BitboardPatternBoard\`), neighbor tracking is accelerated using whole-board 32-bit row bitboards (\`uint[] Occupied\`) and bitwise Chebyshev dilation, completely eliminating adjacency counter tracking from move making and undoing:

\`\`\`csharp
// 1. MakeMove: Update occupied bit in 1 single CPU cycle (0 memory writes for neighbors)
Occupied[by] |= (1u << bx);

// 2. UndoMove: Clear occupied bit in 1 single CPU cycle
Occupied[by] ^= (1u << bx);
\`\`\`

During candidate generation in \`CarbonBitboardAiPlayer\`, row candidate masks are dynamically synthesized across Chebyshev distance 2 via \`DilateRow\`:
\`\`\`csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static uint DilateRow(uint m, uint boardMask)
{
    uint d1 = (m | (m << 1) | (m >> 1)) & boardMask;
    return (d1 | (d1 << 1) | (d1 >> 1)) & boardMask;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public uint GetCandidateMask(int by, ReadOnlySpan<uint> dilatedRows, uint boardMask)
{
    uint mask = 0;
    int y0 = Math.Max(0, by - 2);
    int y1 = Math.Min(BoardHeight - 1, by + 2);
    for (int r = y0; r <= y1; r++) mask |= dilatedRows[r];
    return mask & ~Occupied[by] & boardMask;
}
\`\`\`
This replaces 24 array element writes (\`Adj1++\`/\`Adj2++\`) per node with single-cycle bitwise operations, contributing directly to the **+70% single-threaded search throughput increase** and $2.5\\times$ faster make/undo speed.`;

const newCandidateSection = `## Candidate bounding box & bitboard Chebyshev dilation

In tournament Gomoku, scanning all 225 intersections at every search node is computationally prohibitive. On move 2, evaluating empty squares across the opposite corner of the board produces an intolerable branching factor without offering any tactical value.

\`BitboardPatternBoard\` accelerates candidate generation using two complementary geometric mechanisms:

### 1. The Dynamic Bounding Box
The engine tracks the minimal rectangular bounding box enclosing all placed stones, expanded by a margin of 2 cells in every direction:
$$\\text{UpperLeftCand} = \\big(\\max(4, \\min_i(x_i - 2)), \\ \\max(4, \\min_i(y_i - 2))\\big)$$
$$\\text{LowerRightCand} = \\big(\\min(W + 3, \\max_i(x_i + 2)), \\ \\min(H + 3, \\max_i(y_i + 2))\\big)$$

- On move 1, the bounding box spans only a $5 \\times 5$ window ($25$ cells).
- As stones are played, the box expands dynamically.
- Empty cells outside this bounding box are pruned before tactical evaluation begins.

### 2. Bitboard Chebyshev Dilation (\`DilateRow\` & \`GetCandidateMask\`)
Within the bounding box, an empty square is only tactically relevant if it lies within Chebyshev distance $R \\le 2$ of an existing stone:
$$D_{\\text{Chebyshev}}\\big((x_1, y_1), (x_2, y_2)\\big) = \\max(|x_1 - x_2|, |y_1 - y_2|) \\le 2$$

Rather than maintaining per-cell neighbor counters in memory, \`BitboardPatternBoard\` maintains an array of 32-bit row bitboards (\`uint[] Occupied\`). Stone placement and retraction update the occupied bitboard in a single CPU cycle:
\`\`\`csharp
// 1. MakeMove: Update occupied bit in 1 single CPU cycle (0 memory writes for neighbors)
Occupied[by] |= (1u << bx);

// 2. UndoMove: Clear occupied bit in 1 single CPU cycle
Occupied[by] ^= (1u << bx);
\`\`\`

During candidate generation in \`CarbonBitboardAiPlayer\`, row candidate bitmasks are dynamically generated using two successive bitwise shifts:
\`\`\`csharp
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
\`\`\`
Candidate extraction loops over the non-zero bits using hardware-accelerated trailing-zero-count instructions (\`BitOperations.TrailingZeroCount\`), achieving zero branch mispredictions on non-candidate cells.

> [!NOTE] Architectural Evolution: The Legacy 2-Ring Proximity Counters (\`Adj1\` & \`Adj2\`)
> In the original \`Carbon-Gomoku-master\` (Michał Czardybon, 2001) and baseline \`PatternBoard.cs\`, candidate filtering was tracked by maintaining two concentric counters per cell:
> - \`Adj1\` (8-Neighbor Ring): Count of stones in the 8 Chebyshev distance-1 neighbors:
>   $$\\{(x \\pm 1, y), (x, y \\pm 1), (x \\pm 1, y \\pm 1)\\}$$
> - \`Adj2\` (Ray-Aligned Distance-2 Ring): Count of stones in the 8 ray-aligned distance-2 neighbors:
>   $$\\{(x \\pm 2, y), (x, y \\pm 2), (x \\pm 2, y \\pm 2)\\}$$
>
> On every make and undo operation, Carbon had to iterate and increment/decrement 24 individual cell counters:
> \`\`\`csharp
> Cells[xp - 1, yp - 1].Adj1++; Cells[xp, yp - 1].Adj1++; Cells[xp + 1, yp - 1].Adj1++;
> Cells[xp - 1, yp    ].Adj1++;                           Cells[xp + 1, yp    ].Adj1++;
> Cells[xp - 1, yp + 1].Adj1++; Cells[xp, yp + 1].Adj1++; Cells[xp + 1, yp + 1].Adj1++;
>
> Cells[xp - 2, yp - 2].Adj2++; Cells[xp, yp - 2].Adj2++; Cells[xp + 2, yp - 2].Adj2++;
> Cells[xp - 2, yp    ].Adj2++;                           Cells[xp + 2, yp    ].Adj2++;
> Cells[xp - 2, yp + 2].Adj2++; Cells[xp, yp + 2].Adj2++; Cells[xp + 2, yp + 2].Adj2++;
> \`\`\`
> During candidate generation, cells were filtered with \`c.Adj1 > 0 || c.Adj2 > 0\`. Replacing these 24 array element writes per node with whole-board bitwise Chebyshev dilation increased search throughput by **+70%** and make/undo speed by **$2.5\\times$**.`;

if (!content.includes(oldCandidateSection)) {
  console.log('WARNING: oldCandidateSection not found');
} else {
  content = content.replace(oldCandidateSection, newCandidateSection);
}

// 4. Design notes preallocation update
content = content.replace(
  '- **Fixed-Capacity Preallocation:** `PatternBoard.Cells` is allocated once as `Cell[MaxWidth + 8, MaxHeight + 8]` ($72 \\times 40 = 2,880$ elements). Calling `Start(width, height)` resets the active dimensions without heap allocation, eliminating garbage collection during match play.',
  '- **Fixed-Capacity Preallocation:** `BitboardPatternBoard.Cells` is allocated once as `Cell[Stride * MaxHeightPadded]` ($128 \\times 40 = 5,120$ elements). Calling `Start(width, height)` resets the active dimensions without heap allocation, eliminating garbage collection pauses during search.'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('02-board-and-geometry.md updated successfully.');

