import fs from 'fs';

const filePath = 'docs/brain/README.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Line 32: Iterative deepening depth in flowchart
content = content.replace(
  'Iter["Iterative Deepening<br/>Depth d = 2, 4, 6, ..."]',
  'Iter["Iterative Deepening<br/>Depth d = 1, 2, 3, 4, ..."]'
);

// 2. Line 28: BitboardPatternBoard
content = content.replace(
  'Init["Initialize PatternBoard<br/>Replay placed stones into bitboards"]',
  'Init["Initialize BitboardPatternBoard<br/>Replay placed stones into bitboards"]'
);

// 3. Line 62: Core diagram
content = content.replace(
  'CORE["Gomoku.Core<br/>PatternBoard • CarbonAiPlayer • CarbonTables"]',
  'CORE["Gomoku.Core<br/>BitboardPatternBoard • CarbonBitboardAiPlayer • CarbonTables"]'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('docs/brain/README.md updated successfully.');

