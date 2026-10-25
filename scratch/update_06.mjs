import fs from 'fs';

const filePath = 'docs/brain/06-move-generation-and-ordering.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Bitwise Candidate Extraction snippet
const oldBitwiseSnippet = `\`\`\`csharp
uint mask = pb.GetCandidateMask(by, dilated, boardMask);
while (mask != 0)
{
    int bx = BitOperations.TrailingZeroCount(mask);
    mask &= (mask - 1); // Clear least significant set bit
    int xp = bx + BitboardPatternBoard.Margin;

    ref BitboardPatternBoard.Cell c = ref pb.GetCell(xp, yp);
    short pr = c.Prior();
    if (pr > 1) cnd[nCnd++] = new Cand(xp, yp, pr);
}
\`\`\``;

const newBitwiseSnippet = `\`\`\`csharp
int minY = Math.Max(0, pb.UpperLeftCand.Y - BitboardPatternBoard.Margin);
int maxY = Math.Min(pb.BoardHeight - 1, pb.LowerRightCand.Y - BitboardPatternBoard.Margin);

for (int by = minY; by <= maxY; by++)
{
    uint mask = pb.GetCandidateMask(by, dilated, boardMask);
    int yp = by + BitboardPatternBoard.Margin;

    while (mask != 0)
    {
        int bx = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1); // Clear least significant set bit
        int xp = bx + BitboardPatternBoard.Margin;

        ref BitboardPatternBoard.Cell c = ref pb.GetCell(xp, yp);
        short pr = c.Prior();
        if (pr > 1)
        {
            cnd[nCnd++] = new Cand(xp, yp, pr);
        }
    }
}
\`\`\``;

if (!content.includes(oldBitwiseSnippet)) {
  console.log('WARNING: oldBitwiseSnippet not found');
} else {
  content = content.replace(oldBitwiseSnippet, newBitwiseSnippet);
}

// 2. Prior() method implementation
const oldPriorMethod = `\`\`\`csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public short Prior()
{
    int s = 0;
    for (int k = 0; k < 4; k++)
    {
        s += CarbonTables.Prior[Pattern[k, 0], Pattern[k, 1]];
    }
    return (short)s;
}
\`\`\``;

const newPriorMethod = `\`\`\`csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public short Prior()
{
    return (short)(
        CarbonTables.GetPrior(Pattern00, Pattern01) +
        CarbonTables.GetPrior(Pattern10, Pattern11) +
        CarbonTables.GetPrior(Pattern20, Pattern21) +
        CarbonTables.GetPrior(Pattern30, Pattern31) +
        CarbonTables.GetPrior(Pattern01, Pattern00) +
        CarbonTables.GetPrior(Pattern11, Pattern10) +
        CarbonTables.GetPrior(Pattern21, Pattern20) +
        CarbonTables.GetPrior(Pattern31, Pattern30) + 1);
}
\`\`\``;

if (!content.includes(oldPriorMethod)) {
  console.log('WARNING: oldPriorMethod not found');
} else {
  content = content.replace(oldPriorMethod, newPriorMethod);
}

// 3. Tactical overrides: pb.GetNSt instead of _pb.NSt
const oldTacticalSnippet = `\`\`\`csharp
// 1. Immediate win available: play the winning five
if (_pb.NSt[who, CarbonTables.ThreatA] > 0)
{
    ForceOneCand(cnd, ref nCnd, who, CarbonTables.ThreatA);
    return;
}

// 2. Opponent has an unblocked four: must block the winning square
if (_pb.NSt[opp, CarbonTables.ThreatA] > 0)
{
    ForceOneCand(cnd, ref nCnd, opp, CarbonTables.ThreatA);
    return;
}

// 3. I have an open four: play the unstoppable win
if (_pb.NSt[who, CarbonTables.ThreatB] > 0)
{
    ForceOneCand(cnd, ref nCnd, who, CarbonTables.ThreatB);
    return;
}
\`\`\``;

const newTacticalSnippet = `\`\`\`csharp
// 1. Immediate win available: play the winning five
if (pb.GetNSt(who, CarbonTables.ThreatA) > 0)
{
    ForceOneCand(pb, cnd, ref nCnd, who, CarbonTables.ThreatA);
    return;
}

// 2. Opponent has an unblocked four: must block the winning square
if (pb.GetNSt(opp, CarbonTables.ThreatA) > 0)
{
    ForceOneCand(pb, cnd, ref nCnd, opp, CarbonTables.ThreatA);
    return;
}

// 3. I have an open four: play the unstoppable win
if (pb.GetNSt(who, CarbonTables.ThreatB) > 0)
{
    ForceOneCand(pb, cnd, ref nCnd, who, CarbonTables.ThreatB);
    return;
}
\`\`\``;

if (!content.includes(oldTacticalSnippet)) {
  console.log('WARNING: oldTacticalSnippet not found');
} else {
  content = content.replace(oldTacticalSnippet, newTacticalSnippet);
}

// 4. Data structures table
const oldStructTable = `| Type | Member | Memory | Role |
|---|---|:---:|---|
| \`Cand\` | \`struct (int X, int Y, int Value)\` | 12 bytes | Candidate move with board coordinates and priority score |
| \`PatternBoard.Cell.Adj1\` | \`byte\` | 1 byte | Immediate 8-neighbor stone count |
| \`PatternBoard.Cell.Adj2\` | \`byte\` | 1 byte | 2-ring 16-neighbor stone count |
| \`PatternBoard.UpperLeftCand\` | \`Point\` | 2 bytes | Bounding box minimum coordinate $(x_{\\min}-2, y_{\\min}-2)$ |
| \`PatternBoard.LowerRightCand\`| \`Point\` | 2 bytes | Bounding box maximum coordinate $(x_{\\max}+2, y_{\\max}+2)$ |
| \`CarbonTables.Prior\` | \`short[256, 256]\` | 128 KiB | Static heuristic priority table mapping ray pairs to priority scores |`;

const newStructTable = `| Type | Member | Memory | Role |
|---|---|:---:|---|
| \`Cand\` | \`struct (int X, int Y, int Value)\` | 12 bytes | Candidate move with padded board coordinates and priority score |
| \`BitboardPatternBoard.Occupied\` | \`uint[]\` | 76 bytes | Whole-board 32-bit row bitboards for instant Chebyshev dilation |
| \`BitboardPatternBoard.UpperLeftCand\` | \`Point\` | 8 bytes | Bounding box minimum coordinate $(x_{\\min}-2, y_{\\min}-2)$ |
| \`BitboardPatternBoard.LowerRightCand\`| \`Point\` | 8 bytes | Bounding box maximum coordinate $(x_{\\max}+2, y_{\\max}+2)$ |
| \`CarbonTables.Prior\` | \`short[256, 256]\` | 128 KiB | Static heuristic priority table mapping ray pairs to priority scores |`;

if (!content.includes(oldStructTable)) {
  console.log('WARNING: oldStructTable not found');
} else {
  content = content.replace(oldStructTable, newStructTable);
}

// 5. Fallback expansion & Adjacency text
content = content.replace(
  '3. **Fallback Expansion (Tier 2):** If all candidates have priority $\\le 1$ or if all candidate moves are illegal Renju fouls, the generator falls back to scanning all empty cells with $\\text{Adj1} > 0 \\lor \\text{Adj2} > 0$ with score 0.',
  '3. **Fallback Expansion (Tier 2):** If all candidates have priority $\\le 1$ or if all candidate moves are illegal Renju fouls, the generator falls back to scanning all empty cells within the bounding box with score 0 (up to `MaxCand = 256`).'
);

content = content.replace(
  '- Out of 56 scanned cells, 24 have `Adj1 == 0 && Adj2 == 0` and are skipped in 1 cycle.',
  '- Out of 56 scanned cells inside the bounding box, bitboard Chebyshev dilation (`DilateRow`) identifies 32 candidate cells within Chebyshev distance $\\le 2$ via bitwise operations in sub-microsecond time.'
);

// 6. Where in the code table: update PatternBoard to BitboardPatternBoard
content = content.replace(
  '| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `Cells.Adj1`, `Adj2` | 2-ring candidate proximity counters updated during make/undo |',
  '| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `DilateRow`, `GetCandidateMask` | Bitboard Chebyshev dilation candidate generation |'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('06-move-generation-and-ordering.md updated successfully.');

