import fs from 'fs';

const filePath = 'docs/brain/15-benchmarks-and-performance.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Line 10: Cand[64] -> MaxCand (256)
content = content.replace(
  'Candidate generation uses `stackalloc Cand[64]`, bitboards operate on primitives, and move undo/redo operates via pre-allocated structures.',
  'Candidate generation uses `stackalloc Cand[MaxCand]` (`MaxCand = 256`), bitboards operate on primitives, and move undo/redo operates via pre-allocated structures.'
);

// 2. Line 188: Pre-allocated structures in BitboardPatternBoard
content = content.replace(
  '2. **Pre-allocated Structures in `PatternBoard`:** `RemPlacedPoints[2048]`, `RemMoves[2048]`, and `Cells[28, 28]` are allocated once when the board is initialized and reused for the lifetime of the match.',
  '2. **Pre-allocated Structures in `BitboardPatternBoard`:** `RemPlacedPoints[2048]`, `_remMove[2048]`, `Occupied[31]`, and `Cells[Stride * (MaxHeight + 8)]` are allocated once when the board is initialized and reused for the lifetime of the match.'
);

// 3. Cache diagram & text
const oldCacheSection = `\`\`\`mermaid
flowchart LR
    L1["CPU L1 Data Cache (32 KiB)"]
    Cells["PatternBoard.Cells[28, 28]<br/>784 cells * 36 bytes = 27.5 KiB"]
    Tensor["Threat Tensors &amp; NSt<br/>~1.2 KiB"]
    
    Cells --> L1
    Tensor --> L1
\`\`\`

- **Board Grid Size:** $28 \\times 28 \\text{ cells} \\times 36\\text{ bytes per cell} = 28,224\\text{ bytes} \\approx 27.5\\text{ KiB}$.
- **L1 Cache Capacity:** Modern AMD Zen 4/5 and Intel Raptor Lake cores feature $32\\text{ KiB}$ to $48\\text{ KiB}$ of 8-way associative L1 data cache per core.
- **Result:** The entire active game board resides continuously inside L1 data cache throughout minimax search, yielding zero L1 cache misses during ray pattern bitboard queries.`;

const newCacheSection = `\`\`\`mermaid
flowchart LR
    L1["CPU L1 Data Cache (32 KiB to 48 KiB)"]
    Cells["BitboardPatternBoard Active Grid<br/>23 x 23 cells * 16 bytes = 8.4 KiB"]
    Bitboards["Occupied Row Bitboards<br/>23 * 4 bytes = 92 bytes"]
    Tensor["Threat Tensors &amp; NSt<br/>~1.2 KiB"]
    
    Cells --> L1
    Bitboards --> L1
    Tensor --> L1
\`\`\`

- **Active Grid Footprint:** The playable $15 \\times 15$ grid plus 4-cell margin padding occupies $23 \\times 23 = 529$ cells. At 16 bytes per cell, this is **$8,464\\text{ bytes} \\approx 8.3\\text{ KiB}$**.
- **Row Bitboard Footprint:** 23 32-bit row integers $= 92\\text{ bytes}$.
- **L1 Cache Capacity:** Modern AMD Zen 4/5 and Intel Raptor Lake/Arrow Lake cores feature $32\\text{ KiB}$ to $48\\text{ KiB}$ of 8-way associative L1 data cache per core.
- **Result:** The entire active search board and all threat bitboards easily reside within L1 cache, eliminating L1 data cache misses during minimax tree traversal.

> [!NOTE] Architectural Evolution: Memory Optimization
> The baseline \`PatternBoard\` required $28 \\times 28 \\times 36\\text{ bytes} = 28.2\\text{ KiB}$ for its 2D cell grid with adjacency counters. By stripping \`Adj1/Adj2\` and standardizing on compact 16-byte unmanaged cells, \`BitboardPatternBoard\` reduced active memory footprint by **$70\\%$**, freeing valuable L1 cache space for lookup tables and the transposition table cache line entries.`;

if (!content.includes(oldCacheSection)) {
  console.log('WARNING: oldCacheSection not found');
} else {
  content = content.replace(oldCacheSection, newCacheSection);
}

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('15-benchmarks-and-performance.md updated successfully.');

