import fs from 'fs';

const filePath = 'docs/brain/03-rules-and-variants.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Data structures table
content = content.replace(
  '| [`PatternBoard`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `sealed class` | Evaluates `CheckForbid(x, y)` and `IsForbidden(x, y, player)` |',
  '| [`BitboardPatternBoard`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `sealed class` | Production board evaluating `CheckForbid(x, y)` and `IsForbidden(x, y, player)` |'
);

// 2. Where in the code table
content = content.replace(
  '| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `CheckForbid` | Incremental status evaluation of 3x3, 4x4, and 6+ fouls |',
  '| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `CheckForbid` | Incremental status evaluation of 3x3, 4x4, and 6+ fouls (also in baseline PatternBoard) |'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('03-rules-and-variants.md updated successfully.');

