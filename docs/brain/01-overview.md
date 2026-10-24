# 01 – Overview

[Back to the index](README.md)

## In short

Gomoku .NET is an enterprise-grade Gomoku (Five in a Row) system engineered in C# and targeting modern .NET 10. The engine is inspired by Michał Czardybon's world-class **Carbon Gomoku** (international Gomocup champion). It features 4-axis pattern bitboards with flat 1D indexing, Chebyshev dilation candidate bitmasks, precomputed threat tensors, sub-millisecond tactical Victory by Continuous Fours (VCF) solving, lock-free transposition tables, and scalable **Lazy SMP parallel search** achieving up to **$4,213,000+\text{ NPS}$** ($11.8\times$ scaling on 8 cores). The software is organized following Clean Architecture principles: the zero-dependency domain engine (`Gomoku.Core`) powers a shared cross-platform MVVM application layer (`Gomoku.App`), which drives both a DirectX hardware-accelerated desktop client (`Gomoku.Wpf`) and an Ahead-of-Time (AOT) compiled WebAssembly client with dedicated background Web Worker isolation (`Gomoku.Web`).

---

## The projects

The solution [Gomoku.slnx](https://github.com/cjp-dev/Gomoku/blob/main/Gomoku.slnx) consists of seven targeted projects:

```mermaid
flowchart LR
    Wpf["Gomoku.Wpf<br/>WPF Desktop App<br/>net10.0-windows"] --> Shared["Gomoku.App<br/>ViewModels &amp; Services<br/>net10.0"]
    Web["Gomoku.Web<br/>Blazor WebAssembly App<br/>net10.0"] --> Shared
    Shared --> Core["Gomoku.Core<br/>Domain &amp; Carbon Engine<br/>net10.0"]
    AppTests["Gomoku.App.Tests<br/>ViewModel &amp; Service Tests"] --> Shared
    AppTests --> Core
    CoreTests["Gomoku.Core.Tests<br/>Engine, Rules &amp; AI Tests"] --> Core
    Bench["Gomoku.Benchmark<br/>CLI Performance Tool<br/>net10.0"] --> Core
```

| Project | Target Framework | Role & Contents |
|---|---|---|
| [Gomoku.Core](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core) | `net10.0` | Zero-allocation core engine: board geometries, pattern bitboards, precomputed Carbon lookup tables, VCF solver, Negamax Alpha-Beta search, Zobrist transposition table, opening book, SGF and algebraic serializers. Strictly zero UI dependencies. |
| [Gomoku.App](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.App) | `net10.0` | Shared MVVM application layer using [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/): view models (`MainViewModel`, `SettingsViewModel`, `IntersectionViewModel`, `AnalysisViewModel`), service abstractions (`IDialogService`, `IGameFileService`, `ISoundService`), and procedural 16-bit PCM audio synthesis (`SoundWaves`). |
| [Gomoku.Wpf](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Wpf) | `net10.0-windows` | Hardware-accelerated desktop application: Japanese Kaya wood board rendering, dynamic coordinate grids, interactive hover ghost stones, Renju foul markers, AI telemetry dashboard, and native dialogs. |
| [Gomoku.Web](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Web) | `net10.0` | Client-side Blazor WebAssembly single-page application: runs the full C# engine in the browser sandbox with Ahead-of-Time (AOT) compilation and dedicated Web Worker isolation (`engine-worker.js` delivering 540,000 NPS off the main UI thread), offline Progressive Web App (PWA) support, and embedded markdown architecture documentation viewer. |
| [Gomoku.Benchmark](https://github.com/cjp-dev/Gomoku/blob/main/tools/Gomoku.Benchmark) | `net10.0` | Console performance and profiling tool: throughput measurements for make/undo operations, transposition table masking speed, and nodes-per-second (NPS) scaling across depths. |
| [Gomoku.Core.Tests](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.Core.Tests) | `net10.0` | xUnit test suite for the engine: win detection, Exact5 overline rules, Renju foul algorithms (3x3, 4x4, 6+), Zobrist incremental hash consistency, VCF solving, opening book symmetry, and SGF round-tripping. |
| [Gomoku.App.Tests](https://github.com/cjp-dev/Gomoku/blob/main/tests/Gomoku.App.Tests) | `net10.0` | xUnit test suite for the application layer: board reset, human stone placement, turn sequencing, undo/redo stacks, Settings persistence, sound muting, and Renju forbidden marker UI updates. |

---

## The engine at a glance

The engine's types are divided into five functional areas:

| Group | Main Types | Role |
|---|---|---|
| **Board &amp; Geometry** | [`BoardState`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/BoardState.cs)<br/>[`BoardSize`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/BoardSize.cs)<br/>[`Point`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/Point.cs)<br/>[`PieceColor`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/PieceColor.cs) | Board state representation, standard dimensions ($15 \times 15$, $19 \times 19$, $20 \times 20$), star points, and cell colors (`Empty`, `Black`, `White`, `Wrong`). |
| **Rules &amp; Legality** | [`IRuleEngine`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/IRuleEngine.cs)<br/>[`RuleEngine`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/RuleEngine.cs)<br/>[`GomokuVariant`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/GomokuVariant.cs) | Move validation, winning line detection, and variant enforcement: Freestyle (5+ wins), Exact5 (exact 5 wins), and Renju (asymmetrical foul rules for Black). |
| **Pattern Bitboards** | [`PatternBoard`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs)<br/>[`PatternBoard.Cell`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs)<br/>[`CarbonTables`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonTables.Generated.cs) | 4-direction 8-cell ray bitmasks, directional line threat lookups (`STATUS1`), composite 4-way threat tensor (`STATUS4`), tactical candidate scoring (`Prior`), and stone ranking (`Rank`). |
| **Search &amp; AI** | [`CarbonAiPlayer`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs)<br/>[`SimpleAiPlayer`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/SimpleAiPlayer.cs)<br/>[`TranspositionTable`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/TranspositionTable.cs)<br/>[`Zobrist`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/Zobrist.cs)<br/>[`SearchLimits`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/SearchLimits.cs) | Negamax Alpha-Beta search on closed intervals $[\alpha, \beta]$, iterative deepening, VCF solver (`QuickWinSearch`), candidate generation with `ONE_CAND` pruning, 16-byte packed transposition entries, and time control. |
| **Opening Theory &amp; Formats** | [`OpeningBook`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/OpeningBook.cs)<br/>[`SgfSerializer`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Formats/SgfFormat.cs)<br/>[`GomokuJsonSerializer`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Formats/GomokuRecordFormat.cs)<br/>[`GameFileSerializer`](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Formats/GameFileSerializer.cs) | Dihedral group $D_4$ opening book matching across 67 canonical pattern clusters, SGF `GM[11]` export/import, and human-readable `.gomoku` algebraic coordinate notation. |

---

## Class diagrams

### Core Engine Architecture

```mermaid
classDiagram
    class BoardState {
        +BoardSize Size
        +GomokuVariant Variant
        +PieceColor SideToMove
        +PieceColor FirstPlayer
        +int MoveCount
        +ulong Hash
        +GameStatus Status
        +GetCell(x, y) PieceColor
        +Clone() BoardState
    }

    class PatternBoard {
        +Cell[,] Cells
        +int BoardWidth
        +int BoardHeight
        +GomokuVariant Variant
        +PieceColor Who
        +ulong ZobristKey
        +int[,] NSt
        +Point UpperLeftCand
        +Point LowerRightCand
        +Start(width, height, variant, firstPlayer)
        +MakeMove(x, y)
        +MakePaddedMove(xp, yp)
        +UndoMove()
        +CheckForbid(x, y)
        +IsForbidden(x, y, player) bool
    }

    class Cell {
        +PieceColor Piece
        +byte Pattern00, Pattern01
        +byte Status1_00, Status1_01
        +byte Status4_0, Status4_1
        +sbyte Adj1, Adj2
        +Update1(k, exact5)
        +Update4()
        +Prior() short
    }

    class CarbonAiPlayer {
        +TranspositionTable Table
        +GetMoveAsync(state, limits, legalMoves, progress, token) ValueTask~Move~
        -MinimaxRootAsync(depth, token) ValueTask~Cand~
        -Minimax(h, alpha, beta, token) Cand
        -QuickWinSearch() int
        -GenerateCand(cnd, nCnd, ttBest)
        -ForceOneCand(cnd, nCnd, player, targetStatus)
        -Evaluate() int
    }

    class SimpleAiPlayer {
        +GetMoveAsync(state, limits, legalMoves, progress, token) ValueTask~Move~
        -EvalCell(state, x, y, who) int
        -CountStonesIn5(state, startX, startY, dx, dy, who) int
    }

    class TranspositionTable {
        +int Capacity
        +long Hits
        +TryProbe(key, entry) bool
        +Store(key, depth, score, bound, bestMove)
        +NewSearch()
        +Clear()
    }

    PatternBoard *-- Cell : (MaxWidth+8) x (MaxHeight+8)
    CarbonAiPlayer *-- PatternBoard : incremental board
    CarbonAiPlayer *-- TranspositionTable : cache
    BoardState <.. CarbonAiPlayer : input position
    BoardState <.. SimpleAiPlayer : input position
```

### Application & MVVM Architecture

```mermaid
classDiagram
    class GameSession {
        +BoardState CurrentState
        +IReadOnlyList~Move~ MoveHistory
        +bool CanUndo
        +bool CanRedo
        +ApplyMove(move) bool
        +Undo() bool
        +Redo() bool
        +Reset(size, variant, firstPlayer)
    }

    class MainViewModel {
        +GameSession Session
        +GameSettings Settings
        +AnalysisViewModel Analysis
        +ObservableCollection~IntersectionViewModel~ Intersections
        +bool IsThinking
        +bool IsGameModified
        +bool IsStatusModified
        +string StatusText
        +TimeSpan BlackRemainingTime
        +TimeSpan WhiteRemainingTime
        +ResetBoard(size, triggerAi)
        +OnIntersectionClicked(cell)
        +Undo()
        +Redo()
        +MoveNow()
        +SwitchSides()
        +OpenSettingsAsync()
    }

    class IntersectionViewModel {
        +int X
        +int Y
        +PieceColor Piece
        +bool IsLastMove
        +bool IsWinningLine
        +bool IsHovered
        +bool IsForbidden
        +bool ShowBlackGhost
        +bool ShowWhiteGhost
        +bool ShowForbiddenMarker
        +ClickCommand
    }

    MainViewModel *-- GameSession
    MainViewModel *-- IntersectionViewModel : grid cells
```

---

## The life of one computer move

The following sequence traces the entire execution pipeline when it becomes the computer's turn:

```mermaid
sequenceDiagram
    autonumber
    actor Player as Human Player
    participant VM as MainViewModel (UI Thread)
    participant Worker as Task.Run (Worker Thread)
    participant AI as CarbonAiPlayer
    participant Book as OpeningBook
    participant PB as PatternBoard
    participant TT as TranspositionTable
    participant Sound as ISoundService

    Player->>VM: Clicks Intersection (x, y)
    VM->>VM: Session.ApplyMove(move)
    VM->>Sound: PlaySoundAsync(StonePlaced)
    VM->>VM: UpdateUiState() (check Renju fouls, clocks, game over)
    
    alt Is AI Turn
        VM->>Worker: Task.Run(() => AI.GetMoveAsync(state, limits, legalMoves))
        Worker->>AI: GetMoveAsync(...)
        
        opt MoveCount == 0
            AI-->>VM: Play Geometric Center (H8 / J10)
        end

        opt MoveCount in [1..6] & Book Enabled
            AI->>Book: TryGetMove(pb, out bookPoint)
            Book-->>AI: Found book move (Dihedral D4 match)
            AI-->>VM: Return Book Move
        end

        AI->>PB: Start() & Replay Placed Stones
        AI->>TT: NewSearch() (increment age, reset stats)
        
        loop Iterative Deepening (Depth d = 2, 4, 6, ...)
            AI->>AI: QuickWinSearch() (VCF tactical check)
            AI->>TT: TryProbe(zobristKey, out entry)
            AI->>AI: GenerateCand() (bounding box + ONE_CAND)
            AI->>AI: Minimax(d, root, alpha, beta)
            AI-->>VM: Progress.Report(SearchAnalysis) (Depth, Score, NPS, PV)
            Note over VM: Updates live analysis card on UI thread
        end
        
        AI-->>Worker: Chosen Move
        Worker-->>VM: Chosen Move
        VM->>VM: Session.ApplyMove(bestMove)
        VM->>Sound: PlaySoundAsync(StonePlaced)
        VM->>VM: UpdateUiState()
    end
```

---

## Design principles

1. **Zero-Allocation Search Loops:**
   - In `CarbonAiPlayer.Minimax`, candidate lists (`Span<Cand> cnd = stackalloc Cand[MaxCand]`) and ray bitboards are stack-allocated or struct-based. No managed objects are allocated during search loops, preventing garbage collector pauses during competitive play.
2. **Sentinel-Padded Geometry:**
   - The board allocates a 4-cell outer perimeter filled with `PieceColor.Wrong`. Ray iterations never require bounds-checking conditionals (`x >= 0 && x < width`), maximizing branch prediction efficiency.
3. **Strict Dependency Inversion:**
   - `Gomoku.Core` has **zero** dependencies on UI frameworks, windowing systems, or operating system APIs. All UI interactions (dialogs, file picking, audio playback) are abstracted behind interfaces in `Gomoku.App`.
4. **Deterministic Incremental Hashing:**
   - Transposition keys are maintained incrementally in $O(1)$ time via 64-bit Zobrist XOR operations on every stone placement and undo.
5. **Exact Game Rules Fidelity:**
   - All three standard variants (Freestyle, Exact5, and Renju) are modeled with strict mathematical rigor. Renju fouls (double-three, double-four, overlines) are evaluated recursively to prevent Black from making illegal plays.

---

## Some numbers

| Metric | Measurement / Specification |
|---|---|
| **Board cell array** | Standard $15 \times 15$ tournament geometry ($23 \times 23$ sentinel-padded array in flat 1D layout) |
| **Cell memory footprint** | 16 bytes per cell in `PatternBoard.Cell` (bitboards, status values, proximity counters) |
| **Transposition entry** | Exactly 16 bytes packed (`TranspositionEntry`), 4 entries per 64-byte CPU cache line |
| **Default Transposition Table** | $2^{22} = 4,194,304$ entries ($64 \text{ MiB}$ of RAM), user-configurable from $1\text{ MB}$ to $256\text{ MB}$ |
| **Precomputed table sizes** | `STATUS1` ($256 \times 256$), `STATUS4` ($10,000$ shorts), `Prior` ($256 \times 256$), `Rank` ($106$ bytes) |
| **PatternBoard throughput** | $> 1,300,000$ make/undo operations per second (single core) |
| **Transposition Table probe speed** | $> 170,000,000$ probes per second with power-of-two bitwise mask and lock-free atomic updates |
| **Carbon AI search speed (1 Thread)** | $\approx 540,000$ nodes per second (.NET 10 desktop & Web Worker) |
| **Carbon AI search speed (8 Threads)**| **$4,213,000+$ nodes per second** ($11.8\times$ baseline throughput via Lazy SMP) |
| **Simple AI search speed** | Instantaneous ($< 1\text{ ms}$, non-searching heuristic evaluation) |
| **Unit test suite** | **83 automated unit tests** across Core and App (`0` warnings, `0` errors) |

---

## Where in the code

| File | Description |
|---|---|
| [BoardState.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Models/BoardState.cs) | Immutable board state model and cell array |
| [RuleEngine.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/RuleEngine.cs) | Variant rule definitions and win-line checking |
| [BitboardPatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/BitboardPatternBoard.cs) | 4-ray pattern bitboard with flat 1D memory and Chebyshev dilation candidate bitmasks |
| [PatternBoard.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/Engine/PatternBoard.cs) | Classical 4-ray pattern bitboard and candidate tracking |
| [CarbonTables.Generated.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonTables.Generated.cs) | Precomputed pattern and threat tensors |
| [CarbonBitboardAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonBitboardAiPlayer.cs) | Primary high-performance AI: Lazy SMP multi-threading, VCF solver, candidate ordering, and live telemetry |
| [CarbonAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/CarbonAiPlayer.cs) | Reference Minimax search, VCF solver, candidate ordering, and live telemetry |
| [SimpleAiPlayer.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/SimpleAiPlayer.cs) | Fast 1-ply heuristic pattern evaluator for casual gameplay |
| [LockFreeTranspositionTable.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/LockFreeTranspositionTable.cs) | 16-byte aligned lock-free hash table supporting parallel Lazy SMP |
| [TranspositionTable.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/TranspositionTable.cs) | 16-byte packed hash table |
| [OpeningBook.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.Core/AI/OpeningBook.cs) | Dihedral symmetry opening book matching with direct bitboard queries |
| [MainViewModel.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.App/ViewModels/MainViewModel.cs) | Application state, AI coordination, multi-threading settings, and clocks |
| [AnalysisViewModel.cs](https://github.com/cjp-dev/Gomoku/blob/main/src/Gomoku.App/ViewModels/AnalysisViewModel.cs) | Real-time search telemetry formatting and analysis presentation |

---

Previous: [Index](README.md) · Next: [02 Board and geometry](02-board-and-geometry.md)

