import fs from 'fs';

const filePath = 'docs/brain/07-tactical-vcf-search.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Line 207: _pb to pb
content = content.replace(
  'if (_pb.Variant == GomokuVariant.Renju && _pb.Who == _pb.FirstPlayer &&',
  'if (pb.Variant == GomokuVariant.Renju && pb.Who == pb.FirstPlayer &&'
);

// 2. Data structures table
content = content.replace(
  '| `PatternBoard.NSt` | `int[2, 10]` | 80 bytes | Real-time threat counters by player and threat category |\n| `PatternBoard.Cell.GetStatus4` | `byte` | 1 byte | Cell threat classification (`ThreatA` through `ThreatForbid`) |',
  '| `BitboardPatternBoard.NSt` | `int[32]` | 128 bytes | Real-time threat tensor indexed by `(player << 4) | grade` |\n| `BitboardPatternBoard.Cell.GetStatus4` | `byte` | 1 byte | Cell threat classification (`ThreatA` through `ThreatForbid`) |'
);

// 3. Where in the code table
content = content.replace(
  '| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | `NSt` | Live $2 \\times 10$ threat state tensor updated incrementally on make/undo |',
  '| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | `NSt` | Live 32-element threat state tensor updated incrementally on make/undo |'
);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('07-tactical-vcf-search.md updated successfully.');

