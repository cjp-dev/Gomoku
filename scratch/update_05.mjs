import fs from 'fs';

const filePath = 'docs/brain/05-evaluation.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Line 47: 19x19 to 15x15
content = content.replace(
  'On a $19 \\times 19$ board on move 12, this inspects only 12 cells instead of looping over all 361 intersections.',
  'On a $15 \\times 15$ board on move 12, this inspects only 12 cells instead of looping over all 225 intersections.'
);

// 2. Line 53: _pb.Who to pb.Who
content = content.replace(
  'relative to the player whose turn it is to move (`_pb.Who`):',
  'relative to the player whose turn it is to move (`pb.Who`):'
);

// 3. Line 103: BitboardPatternBoard live threat counter
content = content.replace(
  'Beyond the leaf evaluation, the live threat counter `PatternBoard.NSt[player, grade]` acts as a real-time heuristic radar throughout the entire search tree:',
  'Beyond the leaf evaluation, the live threat tensor `BitboardPatternBoard.GetNSt(player, grade)` acts as a real-time heuristic radar throughout the entire search tree:'
);

// 4. Data structures table
content = content.replace(
  '| `PatternBoard.RemPlacedPoints`| `Point[2048]` | 16 KiB | Stack of placed stone coordinates for $O(\\text{plies})$ leaf evaluation |\n| `PatternBoard.NSt` | `int[2, 10]` | 80 bytes | Live board threat counts by player and threat grade |',
  '| `BitboardPatternBoard.RemPlacedPoints`| `Point[2048]` | 16 KiB | Stack of placed stone coordinates for $O(\\text{plies})$ leaf evaluation |\n| `BitboardPatternBoard.NSt` | `int[32]` | 128 bytes | Live flat board threat counts by `(player << 4) | grade` |'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('05-evaluation.md updated successfully.');

