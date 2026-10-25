import fs from 'fs';
import path from 'path';

const dir = 'docs/brain';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.md')).sort();

// Core files in src/Gomoku.Core
function getAllFiles(dirPath, arrayOfFiles = []) {
  const entries = fs.readdirSync(dirPath, { withFileTypes: true });
  entries.forEach(entry => {
    const full = path.join(dirPath, entry.name);
    if (entry.isDirectory()) {
      getAllFiles(full, arrayOfFiles);
    } else if (entry.name.endsWith('.cs')) {
      arrayOfFiles.push(full);
    }
  });
  return arrayOfFiles;
}

const csharpFiles = getAllFiles('src');
const allCode = csharpFiles.map(f => fs.readFileSync(f, 'utf8')).join('\n');

console.log(`Loaded ${csharpFiles.length} C# source files across src/.`);

// Check suspicious keywords in docs/brain
const suspiciousPatterns = [
  { pattern: /depth\s*\+=\s*2/g, desc: "Step-by-2 iterative deepening (depth += 2)" },
  { pattern: /Adj1/g, desc: "Obsolete Moore adjacency counter Adj1" },
  { pattern: /Adj2/g, desc: "Obsolete Moore adjacency counter Adj2" },
  { pattern: /19\s*[\u00D7x]\s*19/g, desc: "Reference to 19x19 board" },
  { pattern: /PatternBoard\.Cell/g, desc: "Obsolete PatternBoard.Cell reference" },
  { pattern: /stackalloc\s+Cand\[64\]/g, desc: "Old stackalloc Cand[64] buffer capacity" },
  { pattern: /MaxCand\s*=\s*64/g, desc: "Old MaxCand = 64" },
  { pattern: /20\s*[\u00D7x]\s*20/g, desc: "Reference to 20x20 board" },
  { pattern: /vcf_search/g, desc: "HUST Gomoku-main method name vcf_search in C#" },
  { pattern: /PlayRange/g, desc: "HUST Gomoku-main struct PlayRange in C#" }
];

files.forEach(f => {
  const content = fs.readFileSync(path.join(dir, f), 'utf8');
  suspiciousPatterns.forEach(sp => {
    const matches = [...content.matchAll(sp.pattern)];
    if (matches.length > 0) {
      console.log(`[ALERT] ${f}: ${matches.length} matches for "${sp.desc}"`);
    }
  });
});

