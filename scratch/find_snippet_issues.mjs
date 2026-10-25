import fs from 'fs';
import path from 'path';

const dir = 'docs/brain';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.md')).sort();

files.forEach(f => {
  const content = fs.readFileSync(path.join(dir, f), 'utf8');
  const lines = content.split('\n');
  let inBlock = false;
  let startLine = 0;
  let lang = '';
  let blockLines = [];
  let blockIndex = 0;

  lines.forEach((l, idx) => {
    const trimmed = l.trim();
    if (trimmed.startsWith('```')) {
      if (!inBlock) {
        inBlock = true;
        lang = trimmed.substring(3).trim();
        startLine = idx + 1;
        blockLines = [];
      } else {
        inBlock = false;
        blockIndex++;
        if (lang === 'csharp' || lang === 'cs') {
          // Check for known discrepancies
          const text = blockLines.join('\n');
          const issues = [];
          if (text.includes('PatternBoard.Cell') || text.includes('ref PatternBoard')) issues.push('PatternBoard.Cell');
          if (text.includes('Adj1') || text.includes('Adj2')) issues.push('Adj1/Adj2');
          if (text.includes('depth += 2')) issues.push('depth += 2');
          if (text.includes('stackalloc Cand[64]')) issues.push('Cand[64]');
          if (text.includes('19')) issues.push('19 board size');
          if (text.includes('c.Adj1')) issues.push('c.Adj1');
          if (issues.length > 0) {
            console.log(`[ISSUE] ${f} Block #${blockIndex} (L${startLine}-L${idx+1}): ${issues.join(', ')}`);
          }
        }
      }
    } else if (inBlock) {
      blockLines.push(l);
    }
  });
});

