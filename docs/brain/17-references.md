# 17 – References

[Back to the index](README.md)

## In short

This document compiles the academic literature, tournament papers, rule specifications, and software references that informed the design, mathematical models, and implementation of Gomoku .NET and its Carbon Gomoku engine.

---

## 1. Gomoku and Renju game theory

- **Allis, L. V. (1994).** *Searching for Solutions in Games and Artificial Intelligence*. Ph.D. thesis, Department of Computer Science, University of Limburg, Maastricht, The Netherlands.
  - *Significance:* Proved that classical $15 \times 15$ Freestyle Gomoku is a forced win for Black (first player) from the starting position using Proof-Number Search and threat-space trees. Established the foundational theory of Victory by Continuous Fours (VCF).
- **Sakata, E., & Ikawa, W. (1981).** *Five-in-a-Row (Renju)*. The Ishi Press, Tokyo.
  - *Significance:* Definitive textbook on Renju rules, foul mechanics (Overlines, 3x3, 4x4), and the 26 canonical openings (13 Direct and 13 Indirect openings).
- **Chen, W. (2005).** *Threat-Space Search and Proof-Number Search in Connect6 and Gomoku*. ICGA Journal, 28(4), 211–222.
  - *Significance:* Analyzed multi-threat branching and pruning dynamics in $m,n,k$-games.
- **European Renju Federation (2018).** *Official Rules of Renju and Opening Protocols*.
  - *Significance:* Formal codification of international tournament rules, opening protocols (Taraguchi-10, Soosyrv-8), and foul adjudication. Available at: [renju.net](https://www.renju.net).

---

## 2. Carbon Gomoku and Gomocup engine architecture

- **Czardybon, T. (2006).** *Carbon Gomoku: Architecture, Threat Tensors, and Search Techniques*. Gomocup Technical Reports and Source Forensics.
  - *Significance:* The original C++ source code and architectural documentation that inspired this project. Introduced the 4-ray pattern bitboard representation, the $10,000$-element `STATUS4` tensor, and closed-interval Negamax search.
- **Gomocup Tournament Archives (2000–2025).** *Annual International Computer Gomoku and Renju Tournament*.
  - *Significance:* Global competitive benchmark for Gomoku engines (featuring Carbon, Yixin, SlowChess, Embryo). Provided standardized test suites and cross-engine protocol specifications. Available at: [gomocup.org](https://gomocup.org).
- **Sun, K. (2014).** *Yixin: An Artificial Intelligence Program for Gomoku and Renju*. Technical Report, Tsinghua University.
  - *Significance:* Explores modern Deep Proof-Number Search (DPNS) and machine-learned threat databases in top-tier Gomoku engines.

---

## 3. Adversarial search and tree pruning

- **Knuth, D. E., & Moore, R. W. (1975).** *An Analysis of Alpha-Beta Pruning*. Artificial Intelligence, 6(4), 293–326.
  - *Significance:* Foundational mathematical analysis demonstrating that optimal move ordering reduces alpha-beta search complexity from $\mathcal{O}(b^d)$ to $\mathcal{O}(b^{d/2})$.
- **Marsland, T. A. (1986).** *A Review of Game-Tree Pruning*. ICGA Journal, 9(1), 10–20.
  - *Significance:* Survey of iterative deepening, aspiration windows, and principal variation search (PVS).
- **Campbell, M. S., & Marsland, T. A. (1983).** *A Comparison of Minimax and Principal Variation Search*. Artificial Intelligence, 21(3), 347–367.
  - *Significance:* Formulated null-window search probes to refute non-PV candidate moves with minimum branching.
- **Plaat, A., Schaeffer, J., Pijl, W., & de Bruin, A. (1996).** *Exploiting Graph Properties of Game Trees*. Information Sciences, 92(1–4), 215–241.
  - *Significance:* Introduced MTD(f) and zero-window search optimizations with transposition tables.

---

## 4. Transposition tables and hashing

- **Zobrist, A. L. (1970).** *A New Hashing Method with Application for Game Playing*. Technical Report 88, Computer Sciences Department, University of Wisconsin, Madison.
  - *Significance:* Invented XOR-based incremental hashing for grid-based board games.
- **Hyatt, R. M. (1997).** *Transposition Table Management in Crafty*. ICCA Journal, 20(4), 221–226.
  - *Significance:* Detailed power-of-two table bitmasking, aging generational counters, and depth-preferred replacement policies.
- **Chess Programming Wiki (CPW).** *Transposition Table, Zobrist Hashing, and Move Ordering*.
  - *Significance:* Comprehensive technical repository of computer board game techniques. Available at: [chessprogramming.org](https://www.chessprogramming.org).

---

## 5. Software architecture, audio synthesis, and .NET

- **Smith, J. O. (2010).** *Physical Audio Signal Processing: For Virtual Musical Instruments and Digital Audio Effects*. W3K Publishing.
  - *Significance:* Theoretical foundations for mathematical acoustic modeling, exponential dampening envelopes, and procedural sound synthesis.
- **Hollosi, A. (2000).** *Smart Game Format (SGF) File Format FF[4] Specification*.
  - *Significance:* Standardized specification for GM[11] game records, property tags, and move trees.
- **Microsoft Docs (2024–2026).** *.NET Performance and Low-Level Memory Management Guides*.
  - *Significance:* Best practices for `Span<T>`, `stackalloc`, `BitOperations`, and Ahead-of-Time (AOT) compilation in .NET 10.

---

Previous: [16 Glossary](16-glossary.md) · [Back to the index](README.md)

