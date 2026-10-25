import fs from 'fs';

const filePath = 'docs/brain/16-glossary.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Update Adjacency & add Chebyshev Dilation under C and A
const oldAdj = `#### Adjacency (\`Adj1\`, \`Adj2\`)
The geometric stone proximity counters tracked for each board intersection.
- **\`Adj1\` (1-Ring):** Count of stones in the 8 immediate Moore neighbor cells ($\\max(|dx|, |dy|) = 1$).
- **\`Adj2\` (2-Ring):** Count of stones in the 16 secondary neighbor cells at Chebyshev distance 2 ($\\max(|dx|, |dy|) = 2$).
Cells with $\\text{Adj1} == 0 \\land \\text{Adj2} == 0$ are pruned during candidate move generation.`;

const newAdj = `#### Adjacency (\`Adj1\`, \`Adj2\`) *(Historical Baseline)*
The geometric stone proximity counters tracked for each board intersection in original Carbon Gomoku and baseline \`PatternBoard.cs\`.
- **\`Adj1\` (1-Ring):** Count of stones in the 8 immediate Moore neighbor cells ($\\max(|dx|, |dy|) = 1$).
- **\`Adj2\` (2-Ring):** Count of stones in the 16 secondary neighbor cells at Chebyshev distance 2 ($\\max(|dx|, |dy|) = 2$).
In the modern \`BitboardPatternBoard\` engine, these 24 memory writes per node are replaced by whole-board bitwise **Chebyshev Dilation**.`;

content = content.replace(oldAdj, newAdj);

// Add Chebyshev Dilation under C
const oldC = `### C

#### Candidate Move (\`Cand\`)`;

const newC = `### C

#### Chebyshev Dilation (\`DilateRow\`)
The bitwise morphological dilation technique implemented in \`BitboardPatternBoard\` to generate candidate moves. Each 32-bit row bitboard (\`Occupied\`) is shifted left and right and bitwise OR-ed across Chebyshev distance $R \\le 2$:
$$\\text{Dilate}(m) = (m \\mid (m \\ll 1) \\mid (m \\gg 1))$$
This extracts all moves adjacent to existing stones in sub-microsecond time with zero branch mispredictions, eliminating 24 memory counter writes per search node.

#### Candidate Move (\`Cand\`)`;

content = content.replace(oldC, newC);

// 2. MakeMove / UndoMove
content = content.replace(
  'The complementary pair of operations that place a stone on `PatternBoard` and roll it back, incrementally updating ray bitboards, neighbor adjacency counters, and Zobrist hashes in $\\mathcal{O}(1)$ time.',
  'The complementary pair of operations that place a stone on `BitboardPatternBoard` and roll it back, incrementally updating 4-axis ray bitboards, occupied bitboards, threat tensors (`NSt`), and Zobrist hashes in $\\mathcal{O}(1)$ time.'
);

// 3. NSt Tensor
content = content.replace(
  'The live $2 \\times 10$ integer array maintained incrementally on `PatternBoard`, tracking the exact number of active board threats by player and threat category.',
  'The live 32-element threat tensor array maintained incrementally on `BitboardPatternBoard` (indexed by `(player << 4) | grade`), tracking the exact number of active board threats by player and threat category.'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('16-glossary.md updated successfully.');

