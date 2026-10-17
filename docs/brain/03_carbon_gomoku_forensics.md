# Chapter 3: Carbon Gomoku Engine Forensics

## 3.1 Historical Context of Carbon Gomoku
**Carbon Gomoku** was authored by Polish computer scientist **Michał Czardybon** around 2004–2006. It dominated the international Gomocup tournament due to its blazing move-generation speed, bitboard pattern evaluation, and deep tactical solver (**QuickWinSearch** / VCF).

The original source was written in C++ with Borland C++ Builder and Visual Studio, documented partially in Polish (`Documentation_PL/`).

---

## 3.2 Key Forensic Discoveries
During code archaeology of the `Carbon-Gomoku-master` source repository, several crucial design insights were revealed:

### 1. Board-Size Agnostic Core
While Carbon defaults to a $19 \times 19$ GUI board, its evaluation engine and lookup tables are **100% independent of board size**.
- The internal board uses a flat 2D cell grid with a 4-cell padded margin (`Margin = 4` on all sides).
- Any access outside the playing area encounters `PieceColor.Wrong` (guard value), which acts identically to an impassable board border.
- Consequently, the engine runs seamlessly on $15 \times 15$, $19 \times 19$, $20 \times 20$, or any custom size up to $64 \times 32$.

### 2. Precomputed Tables
Carbon eliminated runtime loops through five massive precomputed static tables:
- `CONFIG`: maps 8-cell local ray patterns $(2^8 \times 2^8 = 65,536$ states$)$ into 107 equivalence classes.
- `COUNT5`: encodes the number of potential five-in-a-row lines passing through each position.
- `PRIOR3`: tactical candidate priority scores based on local stone adjacency.
- `STATUS1`: 1D threat grade $(0..9)$ along a single directional ray.
- `STATUS4`: 4D threat tensor $(10 \times 10 \times 10 \times 10 = 10,000$ combinations$)$ synthesizing the combined threat across all 4 ray directions.

### 3. VCF (Victory by Continuous Fours) Solver
In Gomoku, a series of uninterrupted four-in-a-row threats is known as VCF. Carbon's `quickWinSearch` is a specialized, fast branch-and-bound solver that explores VCF sequences up to dozens of plies deep before the general minimax search even begins. If a forced win exists, Carbon plays it instantaneously.

---

## 3.3 Porting Strategy to Modern .NET 10
| Original C++ Feature | .NET 10 Implementation |
| :--- | :--- |
| Raw pointer arrays `unsigned char*` | Clean `ReadOnlySpan<byte>` and flat static arrays |
| Global mutable state in `AICarbon.cpp` | Encapsulated, instance-safe `PatternBoard` and `CarbonAiPlayer` |
| Manual memory allocation (`malloc`/`free`) | Managed memory with zero GC pressure during search loops |
| 32-bit Windows GUI code | Clean Architecture with WPF and Blazor WebAssembly |
| Hardcoded ASCII tables in `.CPP` files | Automated Roslyn code generation (`CarbonTables.Generated.cs`) |

