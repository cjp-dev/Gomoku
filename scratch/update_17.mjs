import fs from 'fs';

const filePath = 'docs/brain/17-references.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Correct Michał Czardybon's initial (M. instead of T.)
content = content.replace(
  '**Czardybon, T. (2006).**',
  '**Czardybon, M. (2001–2006).**'
);

// 2. Add HUST Group 01 citation
const oldCarbonRef = `## 2. Carbon Gomoku and Gomocup engine architecture

- **Czardybon, M. (2001–2006).** *Carbon Gomoku: Architecture, Threat Tensors, and Search Techniques*. Gomocup Technical Reports and Source Forensics.
  - *Significance:* The original C++ source code and architectural documentation that inspired this project. Introduced the 4-ray pattern bitboard representation, the $10,000$-element \`STATUS4\` tensor, and closed-interval Negamax search.`;

const newCarbonRef = `## 2. Carbon Gomoku, HUST Gomoku, and Gomocup engine architectures

- **Czardybon, M. (2001–2006).** *Carbon Gomoku: Architecture, Threat Tensors, and Search Techniques*. Gomocup Technical Reports and Source Forensics.
  - *Significance:* The original C++ source code and architectural documentation that inspired this project (\`Carbon-Gomoku-master\`). Introduced 4-axis ray bitboards, precomputed threat lookup tables, and tactical VCF search.
- **Dao, M. T., et al. (2025/2026).** *Gomoku AI Engine: 16-Bit SIMD Bitboard Representation and PVS Search*. Course Project Report, Hanoi University of Science & Technology (HUST Group 01).
  - *Significance:* Explored 16-bit register SIMD bitboards and bitwise candidate generation on $15 \\times 15$ boards (\`Gomoku-main\`), inspiring Gomoku .NET's bitboard Chebyshev dilation hybrid architecture.`;

content = content.replace(oldCarbonRef, newCarbonRef);

fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('17-references.md updated successfully.');

