import fs from 'fs';
import path from 'path';

const dir = 'docs/brain';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.md')).sort();

const checkList = [
  { name: 'Adj1/Adj2', regex: /\bAdj[12]\b/g },
  { name: 'PatternBoard.Cell', regex: /PatternBoard\.Cell/g },
  { name: '19x19', regex: /19\s*[\u00D7x]\s*19/g },
  { name: '20x20', regex: /20\s*[\u00D7x]\s*20/g },
  { name: 'Cand[64]', regex: /Cand\[64\]/g },
  { name: 'depth += 2', regex: /depth\s*\+=\s*2/g },
  { name: 'Even-Ply', regex: /Even-Ply/gi }
];

console.log('--- DETAILED SCAN OF DOCS/BRAIN ---');
files.forEach(file => {
  const content = fs.readFileSync(path.join(dir, file), 'utf8');
  const lines = content.split('\n');
  const fileIssues = [];

  lines.forEach((line, idx) => {
    checkList.forEach(item => {
      if (item.regex.test(line)) {
        fileIssues.push({ lineNum: idx + 1, type: item.name, text: line.trim() });
      }
    });
  });

  if (fileIssues.length > 0) {
    console.log(`\n### ${file} (${fileIssues.length} occurrences):`);
    fileIssues.forEach(iss => {
      console.log(`  L${iss.lineNum} [${iss.type}]: ${iss.text.substring(0, 100)}`);
    });
  }
});

