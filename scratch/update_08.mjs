import fs from 'fs';

const filePath = 'docs/brain/08-midgame-search.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Sequence diagram: CarbonBitboardAiPlayer
content = content.replace(
  'participant AI as CarbonAiPlayer',
  'participant AI as CarbonBitboardAiPlayer'
);

// 2. Worked example: show depth 2 -> depth 3 -> depth 4 single-ply progression
const oldExample = `## Worked example: search progression on depth 4

Consider searching move 15 for Black.
1. **Depth 2 Completed:**
   - Evaluated 42 nodes in $0.3\\text{ ms}$.
   - Best move found: **H9** with score $+120$.
   - **H9** is stored in the Transposition Table.
2. **Depth 4 Commences:**
   - \`GenerateCand()\` retrieves **H9** from TT and places it at \`cnd[0]\` with score $10,000$.`;

const newExample = `## Worked example: search progression on depth 4

Consider searching move 15 for Black with single-ply iterative deepening ($d = 2, 3, 4$):
1. **Depth 2 Completed:**
   - Evaluated 42 nodes in $0.3\\text{ ms}$.
   - Best move found: **H9** with score $+120$.
   - **H9** is stored in the Transposition Table.
2. **Depth 3 Completed:**
   - Explores odd-ply refutations in $1.1\\text{ ms}$ (280 nodes).
   - Validates that White cannot create an immediate counter-four.
   - Refined evaluations update TT entries with depth 3 parity flags.
3. **Depth 4 Commences:**
   - \`GenerateCand()\` retrieves **H9** from TT and places it at \`cnd[0]\` with score $10,000$.`;

if (!content.includes(oldExample)) {
  console.log('WARNING: oldExample not found');
} else {
  content = content.replace(oldExample, newExample);
}

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('08-midgame-search.md updated successfully.');

