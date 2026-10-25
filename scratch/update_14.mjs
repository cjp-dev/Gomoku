import fs from 'fs';

const filePath = 'docs/brain/14-gui-and-settings.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Mermaid diagram: update iterative deepening depth
content = content.replace(
  'C_ID["Iterative Deepening<br/>Depth 2, 4, 6... up to 50"]',
  'C_ID["Iterative Deepening<br/>Depth 1, 2, 3, 4... single-ply"]'
);

// 2. Table link: CarbonBitboardAiPlayer.cs
content = content.replace(
  '| [Carbon AI (Bitboard)](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) |',
  '| [Carbon AI (Bitboard)](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) |'
);

// 3. Where in the code table
content = content.replace(
  '| [`CarbonAiPlayer.cs`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) | `CarbonAiPlayer` | Tournament Minimax search engine with iterative deepening and VCF quick-win solver |',
  '| [`CarbonBitboardAiPlayer.cs`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) | `CarbonBitboardAiPlayer` | Primary tournament Minimax search engine with Lazy SMP and bitboard candidate generation |\n| [`CarbonAiPlayer.cs`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) | `CarbonAiPlayer` | Reference single-threaded Minimax search engine with iterative deepening and VCF solver |'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('14-gui-and-settings.md updated successfully.');

