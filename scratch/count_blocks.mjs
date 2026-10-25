import fs from 'fs';
import path from 'path';

const dir = 'docs/brain';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.md'));
let total = 0;
const blockDetails = [];

files.forEach(f => {
  const content = fs.readFileSync(path.join(dir, f), 'utf8');
  const lines = content.split('\n');
  let inBlock = false;
  let count = 0;
  let startLine = 0;
  let lang = '';
  let blockLines = [];

  lines.forEach((l, idx) => {
    const trimmed = l.trim();
    if (trimmed.startsWith('```')) {
      if (!inBlock) {
        inBlock = true;
        lang = trimmed.substring(3).trim();
        startLine = idx + 1;
        blockLines = [];
        count++;
      } else {
        inBlock = false;
        blockDetails.push({
          file: f,
          startLine,
          endLine: idx + 1,
          lang,
          lineCount: blockLines.length,
          preview: blockLines.slice(0, 2).join(' ')
        });
      }
    } else if (inBlock) {
      blockLines.push(l);
    }
  });
  console.log(`${f}: ${count} fenced blocks`);
  total += count;
});

console.log(`\nTotal fenced code blocks across docs/brain: ${total}`);

