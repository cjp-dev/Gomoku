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
        const summary = blockLines.slice(0, 3).map(s => s.trim()).filter(Boolean).join(' | ');
        console.log(`[${f}#B${blockIndex}] L${startLine}-L${idx+1} (${lang || 'text'}): ${summary.substring(0, 80)}`);
      }
    } else if (inBlock) {
      blockLines.push(l);
    }
  });
});

