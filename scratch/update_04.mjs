import fs from 'fs';

const filePath = 'docs/brain/04-pattern-bitboards-and-lookup-tables.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Prior formula update
const oldPriorFormula = `$$\\text{Prior}(x, y) = \\sum_{k=0}^{3} \\Big( \\text{Prior}[\\text{pat}_{k,0}][\\text{pat}_{k,1}] + \\text{Prior}[\\text{pat}_{k,1}][\\text{pat}_{k,0}] \\Big) + \\big[\\text{Adj1} \\neq 0\\big]$$`;

const newPriorFormula = `$$\\text{Prior}(x, y) = \\sum_{k=0}^{3} \\Big( \\text{Prior}[\\text{pat}_{k,0}][\\text{pat}_{k,1}] + \\text{Prior}[\\text{pat}_{k,1}][\\text{pat}_{k,0}] \\Big) + 1$$

> [!NOTE] Architectural Evolution: Priority Offset
> In baseline \`PatternBoard.cs\`, the formula added $+1$ dynamically if $\\text{Adj1} \\neq 0$. In \`BitboardPatternBoard\`, candidate generation already guarantees that any evaluated cell is within Chebyshev distance $\\le 2$ of existing stones via bitboard dilation, so the $+1$ bias is added unconditionally.`;

if (!content.includes(oldPriorFormula)) {
  console.log('WARNING: oldPriorFormula not found');
} else {
  content = content.replace(oldPriorFormula, newPriorFormula);
}

// 2. NSt tracking update: explain flat 32-element array in BitboardPatternBoard
const oldNSt = `## Live threat tracking: \`NSt[2, 10]\`

\`PatternBoard\` maintains a live $2 \\times 10$ integer matrix tracking the total count of squares on the board that currently possess each threat grade for Black and White:
$$\\text{NSt}[\\text{player}, \\text{grade}]$$`;

const newNSt = `## Live threat tracking: \`NSt\` (Threat Tensor)

In \`BitboardPatternBoard\`, live threat counts for Black and White across each threat grade (grades $0 \\dots 9$) are maintained in a flat 32-element array with single-cycle bitwise indexing:
\`\`\`csharp
public readonly int[] NSt = new int[32]; // Flat: (player << 4) | grade
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ref int GetNSt(int player, int grade) => ref NSt[(player << 4) | grade];
\`\`\`
*(In historical \`PatternBoard.cs\`, this was stored as a 2D managed matrix \`int[2, 10]\`)*.`;

if (!content.includes(oldNSt)) {
  console.log('WARNING: oldNSt not found');
} else {
  content = content.replace(oldNSt, newNSt);
}

// 3. Where in the code table
const oldTable = `| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | \`BitboardPatternBoard\` | High-throughput flat 1D pattern board with Chebyshev dilation candidate masks |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | \`PatternBoard.Cell\` | 8-cell bitmasks (\`Pattern00..Pattern31\`), \`Update1\`, \`Update4\`, \`Prior()\` |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | \`MakePaddedMove\` | Incremental ray updates and \`NSt\` threat tracking |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | \`CheckConsistency\` | Verification method ensuring \`NSt\` matches whole-board scans |`;

const newTable = `| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | \`BitboardPatternBoard\` | High-throughput flat 1D pattern board with Chebyshev dilation candidate masks |
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | \`BitboardPatternBoard.Cell\` | 8-cell bitmasks (\`Pattern00..Pattern31\`), \`Update1\`, \`Update4\`, \`Prior()\` |
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | \`MakePaddedMove\` | Incremental ray updates and \`NSt\` threat tracking |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | \`PatternBoard\` | Baseline reference 2D pattern board with \`CheckConsistency\` and \`Adj1/Adj2\` |`;

if (!content.includes(oldTable)) {
  console.log('WARNING: oldTable not found');
} else {
  content = content.replace(oldTable, newTable);
}

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('04-pattern-bitboards-and-lookup-tables.md updated successfully.');

