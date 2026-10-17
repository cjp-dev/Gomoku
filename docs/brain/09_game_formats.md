# Chapter 9: Game Serialization: SGF & Gomoku Formats

## 9.1 Supported File Formats
To ensure maximum interoperability with tournament databases, third-party viewers, and human-readable archives, the system implements dual serialization:

1. **Smart Game Format (SGF) (`.sgf`)**: The international standard for board games (Go, Gomoku, Renju).
2. **Gomoku Coordinate Notation (`.gomoku`, `.txt`)**: Human-readable algebraic coordinate format.

Both formats are unified under an auto-detecting facade: `GameFileSerializer.TryParse()`.

---

## 9.2 Smart Game Format (SGF) Specification
In SGF FF[4], Gomoku uses game type `GM[11]` (or standard `GM[1]` Go coordinates):

```sgf
(;GM[11]FF[4]CA[UTF-8]AP[Gomoku.Net:1.0]SZ[15]RU[Freestyle]PB[Human]PW[Carbon AI]KM[0];B[hh];W[hi];B[ih];W[ii];B[gh])
```

### Key Property Tags:
- `SZ[N]`: Board dimension (e.g. `15`, `19`, `20`).
- `RU[...]`: Gomoku variant (`Freestyle`, `Exact5`, `Renju`).
- `PB[...]` / `PW[...]`: Black and White player names.
- `;B[xy]` / `;W[xy]`: 0-indexed lowercase coordinates (`a` = 0, `b` = 1, `h` = 7, etc.). For instance, `hh` corresponds to coordinate $(7, 7)$ ($H8$ on a 15x15 board).

---

## 9.3 Gomoku Coordinate Record Format (`.gomoku`)
A clean, text-based tournament record format with bracketed metadata headers:

```text
[Game "Gomoku"]
[Size "15x15"]
[Variant "Freestyle"]
[Date "2026-10-17"]
[Black "Human"]
[White "Carbon AI"]
[Result "BlackWon"]

1. H8 H9 2. I8 I9 3. G8 G9
```

- Moves are numbered sequentially by turn: `1. H8 H9` represents Black's move 1 and White's move 1.
- Coordinates follow consecutive letters `A..T` and rows `1..N`.
- Both serializers and parsers are covered by round-trip automated unit tests in `Gomoku.Core.Tests`.

