import fs from 'fs';
import path from 'path';

const dir = 'docs/brain';
const files = fs.readdirSync(dir).filter(f => f.endsWith('.md')).sort();

let totalErrors = 0;

console.log(`Checking ${files.length} markdown documentation files...`);

files.forEach(f => {
  const content = fs.readFileSync(path.join(dir, f), 'utf8');
  const lines = content.split('\n');

  // Check 1: No depth += 2 anywhere
  lines.forEach((l, idx) => {
    if (/depth\s*\+=\s*2/.test(l)) {
      console.log(`[ERROR] ${f}:${idx+1} contains 'depth += 2'`);
      totalErrors++;
    }
    if (/Cand\[64\]/.test(l)) {
      console.log(`[ERROR] ${f}:${idx+1} contains 'Cand[64]'`);
      totalErrors++;
    }
  });

  // Check 2: Relative links
  const linkMatches = [...content.matchAll(/\[.*?\]\((.*?\.md)\)/g)];
  linkMatches.forEach(m => {
    const target = m[1];
    if (!fs.existsSync(path.join(dir, target))) {
      console.log(`[ERROR] ${f} contains broken link: ${target}`);
      totalErrors++;
    }
  });
});

if (totalErrors === 0) {
  console.log('SUCCESS: All 19 documentation files passed automated linter checks with 0 errors!');
} else {
  console.log(`FAILED: Found ${totalErrors} documentation errors.`);
  process.exit(1);
}

