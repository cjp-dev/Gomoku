# Gomoku Game - Project Specification & Roadmap

## 1. Overview & Vision
The goal is to develop an extensible, high-performance **Gomoku (Five in a Row)** application in C# and .NET 10.

The project follows **Clean Architecture** to ensure complete decoupling between the game rules, board state, AI engines, application coordination, and presentation layers.
- **Engine Library:** High-performance, zero-UI class library (`Gomoku.Core`).
- **Shared App Layer:** Shared ViewModels, game loop, file persistence, and settings (`Gomoku.App`), shared between all clients.
- **Primary Desktop Client:** Modern .NET 10 WPF desktop application (`Gomoku.Wpf`) featuring:
  - 60 FPS board rendering of a 19x19 wooden board with traditional star points (*hoshi*), stone drop shadows, and winning 5-in-a-row highlight indicators.
  - Full Game Save / Load system supporting both **SGF (Smart Game Format)** and standard Gomoku coordinate notation with metadata headers.
  - Live Analysis telemetry pane (Move, Depth, Score, Best Move, Nodes, Evaluations/s, Time) matching Stello and Connect-4.
  - Computer Settings modal dialog and time controls matching Stello and Connect-4 (`Fixed depth`, `Time per move`, `Time per game`, AI Engine selection).
- **Web Client:** Blazor WebAssembly application (`Gomoku.Web`) with Ahead-Of-Time (AOT) compilation, sharing the exact same core engine and app layer.
- **Deployment:** Automated CI/CD deploying the Blazor WebAssembly static files to **Azure Static Web Apps** via GitHub Actions, following the deployment method in `C:\Udvikling\Spil\Stello\.github\workflows\azure-static-web-apps.yml`.
- **Documentation:** Comprehensive architectural and engine documentation in `docs/brain/`, following the numbered chapter format and technical depth of `C:\Udvikling\Spil\Connect-4\docs\brain` and `C:\Udvikling\Spil\Stello\docs\brain`. The documentation is served directly inside the web client via Markdig markdown rendering.

### 1.1 Inspiration & Engine Heritage: Carbon Gomoku
The AI engine architecture draws direct inspiration from **Carbon Gomoku**, an acclaimed, highly optimized Gomoku program created by Polish developer **Michał Czardybon** (located in the project folder under `Carbon-Gomoku-master`). 

Carbon Gomoku was recognized as one of the strongest Gomoku engines in the world at its inception (dominating other engines and competing in the Gomocup tournaments). The engine is documented extensively in Polish within the `Carbon-Gomoku-master/Documentation_PL` subfolder. Key insights and mathematical formalisms translated and synthesized from that documentation include:
- **Directional Pattern Bitboards (`pattern[4][2]`):** Encoding the 8 neighboring cells across 4 directions into fast 8-bit masks.
- **Precomputed Directional Lookups:**
  - `STATUS1.CPP`: 256x256 table classifying single-direction ray configurations into 10 pattern classes (0..9).
  - `CONFIG.CPP`: 256x256 table mapping 8-bit pattern combinations of both players to 107 canonical configuration types.
  - `COUNT5.CPP`: 107x5 table tabulating the exact count of 5-in-a-row formations achievable in 1, 2, 3, 4, or 5 moves.
  - `PRIOR3.CPP`: 256x256 table providing fast heuristic cell priority scoring for move ordering.
  - `STATUS4`: 4-dimensional tensor ($10 \times 10 \times 10 \times 10$) mapping the 4 directional statuses into tactical threat categories: Class A (five-in-a-row), Class B (open four), Class C (four + open three), Class D (four + blocked three), Class E (blocked four), Class F (double open three / 3x3 threat), Class G (open three + blocked two), Class H (single open three).
- **Fast Tactical Pruning & Forced Moves (`ONE_CAND`):** Instant recognition and execution of immediate wins (status A), opponent four-blocks (status A), open-four creations (status B), and open-three defensive move restrictions.
- **Quick Win Search (`quickWinSearch`):** Specialized Victory-by-Continuous-Fours (VCF) / tactical threat search executing outside full minimax to instantly verify terminal combinations and mate sequences.
- **Triple-Hash Transposition Table:** Multi-word hash signatures (`hashValA`, `hashValB`, `hashValC` creating an 80-bit collision-free signature) with best-move caching for next-ply move ordering.
- **Iterative Deepening & Dynamic Time Management:** Smooth depth progression from 2 to 50 plies, time-out prevention heuristics (`TIMEOUT_PREVENT = 5`), and game-clock allocation (`MATCH_SPARE = 7`).
- **Baseline AI (`AISimple`):** Fast, non-searching pattern-evaluation AI for casual play.

UI and client application patterns are adapted from `C:\Udvikling\Spil\Stello` and `C:\Udvikling\Spil\Connect-4`.

---

## 2. Game Rules & Mechanics

The game implements the standard rules of Gomoku matching **Carbon Gomoku**:

### 2.1 Board & Pieces
* **Board:** 19x19 grid with 361 intersections total (`(x, y)` coordinates from `(0, 0)` to `(18, 18)`).
* **Grid Markings:** 9 traditional star points (*hoshi*) located at intersections `(3, 3)`, `(3, 9)`, `(3, 15)`, `(9, 3)`, `(9, 9)`, `(9, 15)`, `(15, 3)`, `(15, 9)`, and `(15, 15)` (using 0-based indexing).
* **Pieces:** Black stones and White stones placed on the grid intersections (not inside the squares).
* **First Move:** Black moves first. The first move is typically placed at the center star point `(9, 9)` (*tengen*).

### 2.2 Gameplay Rules
1. Players alternate placing one stone of their color on any vacant intersection.
2. Once placed, stones are never moved, jumped, or captured.
3. **Winning Condition (Freestyle Gomoku - Carbon Default):**
   * The first player to form an unbroken horizontal, vertical, diagonal, or antidiagonal line of **five or more consecutive stones** of their color immediately wins the game.
   * Overlines (lines of 6, 7, or more consecutive stones) count as a win in standard Freestyle Gomoku.
4. **Draw Condition:**
   * If all 361 intersections are filled without either player achieving 5 in a row, the game is declared a draw.
   * Players may also agree to a mutual draw.

### 2.3 Rule Configuration Flags (Mirroring Carbon Gomoku)
Configurable via the Game Settings dialog:
1. **Exact 5 (`info_exact5`):**
   * When enabled (`info_exact5 = 1`), a player must form **exactly 5 stones** in a row to win. Overlines (6 or more stones) do **not** trigger a win.
   * When disabled (`info_exact5 = 0`, default), overlines count as a win (Freestyle Gomoku).
2. **Renju Mode (`info_renju`):**
   * When enabled (`info_renju = 1`), tournament Renju restrictions apply to the first player (Black):
     - Black is **forbidden** from playing Double-Three (3x3), Double-Four (4x4), and Overlines (6+ stones). If Black plays a forbidden move, it is an illegal foul or loss.
     - White has no restrictions and may form 3x3, 4x4, and wins on 5 or overlines (exact logic implemented via Carbon's `checkForbid(x, y)`).
   * When disabled (`info_renju = 0`, default), standard Gomoku rules apply equally to both players.

### 2.4 Game Record & Persistence
Dual support for standard Gomoku formats:
1. **SGF (Smart Game Format, `.sgf`):**
   * The universal standard for Go, Gomoku, and Renju games.
   * Stores game tree metadata (`GM[11]` for Gomoku, `SZ[19]`, `PB[PlayerBlack]`, `PW[PlayerWhite]`, `KM[0]`, `RU[Freestyle]`), and moves in standard column/row letter notation (e.g. `;B[jj];W[ih];B[ji]...`).
2. **Standard Gomoku Coordinate Notation (`.gomoku` / `.txt`):**
   * Human-readable coordinate move list (e.g., `1. J10  2. H9  3. K11 ...` or coordinate pairs `9,9  8,8  10,10`), matching the header tag structure of Stello and Connect-4:
     - `[Game "Gomoku"]`
     - `[Size "19x19"]`
     - `[Variant "Freestyle"]` (or `"Exact5"`, `"Renju"`)
     - `[GameMode "HumanVsComputer"]`
     - `[AIPlayer "CarbonAI"]`
     - `[Depth "6"]`
     - `[SecondsPerMove "5"]`
     - `[MinutesPerGame "5"]`
3. **File Operations:** Standard `New`, `Open...`, `Save`, and `Save As...` with dirty tracking, file dialogs, and window title integration.

---

## 3. System Architecture & Solution Structure

Following the established architecture of `Stello` and `Connect-4`:

```
Gomoku/
├── .github/
│   └── workflows/
│       └── azure-static-web-apps.yml  # CI/CD: build .NET 10, wasm-tools, AOT, deploy to Azure SWA
│
├── docs/
│   └── brain/                         # Numbered markdown chapters for engine and architecture
│       ├── README.md                  # The Gomoku brain on one page + chapter index
│       ├── images/                    # SVG architectural, board, pattern, search, and tensor diagrams
│       ├── 01-overview.md             # Projects, core abstractions, move lifecycle
│       ├── 02-board-and-coordinates.md# 19x19 grid, star points, indexing, neighbor directions
│       ├── 03-rules-and-patterns.md   # Win conditions, 5-in-a-row, overlines, exact 5, Renju fouls
│       ├── 04-game-record.md          # GameSession, undo/redo, SGF format & Gomoku notation
│       ├── 05-evaluation-and-heuristics.md # Pattern tensors, CONFIG, COUNT5, 1..5 move 5-in-a-row potential
│       ├── 06-move-ordering-and-candidates.md # Bounding box, PRIOR tables, ONE_CAND forced moves
│       ├── 07-search-and-quickwin.md  # Minimax Alpha-Beta, iterative deepening, quickWinSearch (VCF)
│       ├── 08-transposition-table.md  # Multi-hash Zobrist signatures, 80-bit keys, replacement policies
│       ├── 09-time-control.md         # Move timers, soft/hard time limits, MATCH_SPARE, TIMEOUT_PREVENT
│       ├── 10-app-integration.md      # Game loop, background tasks, WASM cooperative yielding
│       ├── 11-glossary.md             # Gomoku, Renju, pattern, and AI search terminology
│       └── 12-references.md           # Carbon Gomoku (M. Czardybon), Gomocup literature, game theory
│
├── src/
│   ├── Gomoku.Core/                   # Pure Game Engine & Logic (.NET 10)
│   │   ├── Models/                    # Piece, Point, Move, BoardState, GameStatus, CandidateMove
│   │   ├── Engine/                    # RuleEngine, PatternEngine, ZobristHash, GameSession
│   │   ├── Formats/                   # SgfFormat, GomokuRecordFormat
│   │   └── AI/                        # IPlayer, HumanPlayer, SimpleAiPlayer, CarbonAiPlayer,
│   │                                  # EvaluationFunction, SearchLimits, SearchAnalysis
│   │
│   ├── Gomoku.App/                    # Shared Application Layer (.NET 10)
│   │   ├── Models/                    # GameSettings, GameMode, GameFile
│   │   ├── ViewModels/                # MainViewModel, BoardViewModel, AnalysisViewModel, SettingsViewModel
│   │   ├── Services/                  # ISoundService, IDialogService, IGameFileService
│   │   └── Documentation/             # BrainDocs parser & Markdig chapter loader
│   │
│   ├── Gomoku.Wpf/                    # Desktop Presentation Layer (.NET 10 WPF)
│   │   ├── Views/                     # MainWindow, BoardView, SettingsWindow, LogWindow
│   │   ├── Controls/                  # GomokuBoardControl (vector rendering, star points, stones)
│   │   ├── Services/                  # WpfDialogService, WpfGameFileService, SoundPlayerService
│   │   └── Converters/                # Value converters for XAML bindings
│   │
│   └── Gomoku.Web/                    # Web Presentation Layer (Blazor WebAssembly .NET 10)
│       ├── Pages/                     # Home.razor (Game UI), Docs.razor (Markdig docs viewer)
│       ├── Components/                # BoardComponent, MoveHistoryPanel, GameControls, TelemetryPanel
│       └── wwwroot/                   # CSS, audio, icons, board textures, static web assets
│
├── tests/
│   ├── Gomoku.Core.Tests/             # Unit tests: Rules, 5-in-a-row, Exact5, Renju fouls, SGF, Search, TT
│   └── Gomoku.App.Tests/              # Unit tests: ViewModels, GameSession, File I/O, Settings, Clocks
│
└── tools/
    └── Gomoku.Benchmark/              # CLI empirical benchmark runner for AI search & node metrics
```

### 3.1 `Gomoku.Core` (Engine & Logic)
* **Zero UI Dependencies:** Pure C# 13 / .NET 10 base class libraries only.
* **Deterministic & Pure:** State transitions produce verifiable, cloneable states.
* **Key Components:**
  * `RuleEngine`: Fast board state tracking, legal move verification, incremental 5-in-a-row detection in all 4 directions, and terminal status evaluation.
  * `PatternEngine`: Incremental directional pattern maintenance ported from Carbon's `AICarbonMove.cpp`:
    - Maintains two 8-bit masks (`pattern[4][2]`) for each cell representing relative neighbor states 4 steps in each direction.
    - Precalculated tables (`CONFIG`, `COUNT5`, `PRIOR`, `STATUS1`, `STATUS4`).
  * `IPlayer`: Player abstraction implemented by `HumanPlayer`, `SimpleAiPlayer` (AISimple ported from Carbon), and `CarbonAiPlayer` (AICarbon ported from Carbon).
  * `CarbonAiPlayer`: Alpha-Beta search with **Iterative Deepening**, Zobrist Transposition Table, `quickWinSearch` (VCF), expert tactical heuristics (`ONE_CAND`), time management, and real-time `IProgress<SearchAnalysis>` telemetry streaming.
  * `SgfFormat` & `GomokuRecordFormat`: Comprehensive parser and serializer for SGF collections and coordinate notation.

### 3.2 `Gomoku.App` (Shared Application Layer)
* Shared between `Gomoku.Wpf` and `Gomoku.Web`.
* Hosts the MVVM ViewModels (`MainViewModel`, `BoardViewModel`, `AnalysisViewModel`, `SettingsViewModel`).
* Asynchronous AI game loop with background task execution, cancellation tokens, and live `IProgress<SearchAnalysis>` reporting updating the Analysis pane during active search.
* Chess clock management: tracks remaining time per game, allocates move budgets, and refunds clock time on `Undo`.
* Abstractions for procedural audio (`ISoundService`), dialogs (`IDialogService`), and storage (`IGameFileService`).

### 3.3 `Gomoku.Wpf` (Desktop UI)
* Native Windows desktop UI using WPF and modern clean styling matching Stello and Connect-4.
* 60 FPS responsive vector board rendering with wood grain texture, star points, smooth stone drop animations, last move marker, and winning line highlights.
* Dedicated **Analysis** pane displaying engine telemetry (Move, Depth, Score, Best Move, Nodes, Evaluations/sec, Time) updated live while the computer thinks.
* **Settings Window** dialog with radio toggles and sliders matching Stello and Connect-4.
* Visual indicators: active player badge, chess clocks, thinking progress bar, and evaluation sparklines.

### 3.4 `Gomoku.Web` (Blazor WebAssembly)
* High-performance browser client using Blazor WebAssembly targeting .NET 10 with Ahead-Of-Time (AOT) compilation enabled for Release builds (`<RunAOTCompilation>true</RunAOTCompilation>`).
* Responsive board design inspired by Stello and Connect-4, supporting both desktop and touch devices.
* Integrated `/docs` route rendering `docs/brain` markdown chapters using Markdig.
* Targets Azure Static Web Apps with automated GitHub Actions deployment.

---

## 4. Artificial Intelligence & Computer Settings (Carbon Gomoku Architecture)

### 4.1 Directional Pattern Encoding & Precomputed Lookups
Following the architecture of Carbon Gomoku documented in `Documentation_PL/Analiza.htm`:
1. **Ray Scanning (4 Directions):**
   * For every intersection $(x, y)$, the 4 directions are:
     - Horizontal: $(\Delta x = 1, \Delta y = 0)$
     - Vertical: $(\Delta x = 0, \Delta y = 1)$
     - Main Diagonal: $(\Delta x = 1, \Delta y = 1)$
     - Anti-Diagonal: $(\Delta x = 1, \Delta y = -1)$
   * In each direction $k \in \{0, 1, 2, 3\}$, 4 steps forward and 4 steps backward (8 neighboring intersections) determine the pattern around $(x, y)$.
2. **8-Bit Pattern Representation (`pattern[4][2]`):**
   * For player $P \in \{0, 1\}$, the presence of player $P$'s stones in the 8 neighboring cells is packed into an 8-bit `byte`:
     - Low nibble (bits 0..3): 4 cells backward (distances 1 to 4).
     - High nibble (bits 4..7): 4 cells forward (distances 1 to 4).
   * Incremental updates on stone placement and undo ensure $O(1)$ maintenance (updating only the 8 cells along each of the 4 lines through $(x, y)$).
3. **Lookup Tables:**
   * **`STATUS1[256][256]` (Single-Direction Status):** Classifies the ray pattern into 10 fundamental classes (0 to 9):
     - `9`: Open Four (`.OOOO.`)
     - `8`: Open Three (`..OOO.`)
     - `7`: Blocked Three / Four (`XOOO.`)
     - `6`: Open Two (`...OO.`)
     - `5`: Blocked Two (`..XOO.`)
     - `4`: Open One (`....O.`)
     - `3`: Blocked One (`...XO.`)
     - `2`: Empty line (`......`)
     - `1`: Opponent stone (`....X.`)
     - `0`: Blocked / Dead line
   * **`STATUS4[10][10][10][10]` (Tactical Intersection Classification):** Combines the four directional `STATUS1` ratings into a single hierarchical threat grade for the cell:
     - Grade `A` (Immediate Win): 5-in-a-row available.
     - Grade `B` (Unstoppable Four): Open four or dual blocked fours.
     - Grade `C` (Four + Open Three): Dual win threat.
     - Grade `D` (Four + Blocked Three/Two).
     - Grade `E` (Single Blocked Four).
     - Grade `F` (Double Open Three - 3x3): Lethal fork threat.
     - Grade `G` (Open Three + Minor threat).
     - Grade `H` (Single Open Three).
   * **`CONFIG[256][256]` & `COUNT5[107][5]` (Evaluation Tables):**
     - Maps directional patterns of both players into one of 107 canonical configuration states.
     - `COUNT5[cfg][m]` stores how many distinct 5-in-a-row alignments can be completed in $m \in \{1, 2, 3, 4, 5\}$ moves through that cell.
   * **`PRIOR[256][256]` (Move Ordering Priority):**
     - Precomputed heuristic priority score used to sort candidate moves before search.

### 4.2 Heuristic Evaluation Function ($H(s)$)
Ported from `AICarbon::evaluate()`:
* The evaluation of board state $s$ is computed as the sum of potentials for all stones placed on the board:
  $$H(s) = \sum_{c \in \text{Stones}_{\text{Me}}} \text{Score}(c) - \sum_{c \in \text{Stones}_{\text{Opp}}} \text{Score}(c)$$
* For each stone $c$, its score across all 4 directions is calculated using the weighted 5-completion vector:
  $$\text{Score}(c) = \sum_{k=0}^3 \sum_{m=0}^4 \text{weight}[m] \times \text{COUNT5}[\text{CONFIG}(c, k)][m]$$
  where the completion weights are:
  - $\text{weight}[4] = 19$ points (winning five in 1 move)
  - $\text{weight}[3] = 15$ points (winning five in 2 moves)
  - $\text{weight}[2] = 11$ points (winning five in 3 moves)
  - $\text{weight}[1] = 7$ points (winning five in 4 moves)
  - $\text{weight}[0] = 3$ points (winning five in 5 moves)
* Terminal scores: $\text{WIN\_MAX} = 30,000$ points. A win in $n$ plies evaluates to $\text{WIN\_MAX} - n$ to favor rapid victories and delay inevitable defeats.

### 4.3 Move Generation, Bounding Box & Expert Pruning
To prevent exploring all 361 intersections at every node:
1. **Dynamic Bounding Box:**
   * Search is strictly confined to a bounding box extending **2 cells beyond the outermost occupied stones**:
     $$\text{SearchBox} = [\min(X) - 2, \max(X) + 2] \times [\min(Y) - 2, \max(Y) + 2]$$
   * Bounding coordinates (`upperLeftCand`, `lowerRightCand`) are incrementally expanded on moves and contracted on undos.
2. **Adjacency Requirement:**
   * Only empty cells with `adj1 > 0` (neighbor at distance 1) or `adj2 > 0` (neighbor at distance 2) are admitted as candidate moves.
   * Moves with heuristic `prior() <= 1` are discarded during non-quiescent phases.
3. **Expert Tactical Overrides (`ONE_CAND`):**
   * Before launching minimax branching, the engine checks global status counters $nSt[\text{player}][\text{grade}]$:
     - **Make Win:** If $nSt[\text{Me}][A] > 0$, immediately return the single winning move ($nCnd = 1$).
     - **Block Four:** If $nSt[\text{Opp}][A] > 0$, immediately force candidate list to the blocking move ($nCnd = 1$).
     - **Make Open Four:** If $nSt[\text{Me}][B] > 0$, immediately play the open four ($nCnd = 1$).
     - **Defend Open Three:** If $nSt[\text{Opp}][B] > 0$, prune candidate list strictly to moves that counter the open three.

### 4.4 Quick Win Search (`quickWinSearch` / VCF)
Ported from `AICarbon::quickWinSearch()`:
* A lightweight recursive tactical solver that searches solely for **Victory by Continuous Fours (VCF)** and immediate forced responses:
  - Detects if an immediate win exists (returns $+1$).
  - Detects if the opponent has two independent four-threats that cannot both be blocked (returns $-2$).
  - If the opponent has a single four-threat, simulates the forced blocking move and recursively continues threat exploration.
  - Recognizes killer open-four and four-plus-three tactical formations (grades B, C, F) without incurring full minimax branch explosion.

### 4.5 Search Algorithm: Minimax with Alpha-Beta & Iterative Deepening
* **Algorithm:** Negamax formulation of Minimax with Alpha-Beta pruning ($\alpha$-$\beta$).
* **Iterative Deepening:** Searches plies $D = 2, 3, 4, \dots, D_{\max}$ (up to depth 50), utilizing the Principal Variation (PV) best move retrieved from the Transposition Table to guide move ordering at depth $D+1$.
* **Move Ordering:**
  1. Hash move from Transposition Table (examined first).
  2. Tactical overrides (Grade A/B threat moves).
  3. Quicksort sorting candidates by descending precomputed `prior()` scores.

### 4.6 Transposition Table & Zobrist Signatures
To handle transpositions resulting from identical stone configurations reached via different move permutations:
* **Multi-Hash Zobrist Signatures:**
  - 3 independent pseudo-random numbers per intersection and player color: `hashValA`, `hashValB`, `hashValC` (each 32-bit).
  - `hashValA % tableSize` indexes the table bucket.
  - `hashValB` and `hashValC` form a combined 64-bit verification signature to ensure virtually zero hash collisions.
* **Compact Transposition Entry:**
  - Signature (`hashB`, `hashC`), 16-bit score (`value`), 8-bit depth (`depth`), 8-bit bounds flag, and best move coordinates (`bestX`, `bestY`).
* **Capacity & Sizing:**
  - Configurable from $2^{16}$ ($65,536$) entries up to $2^{24}$ ($16,777,216$) entries.
  - Dynamic resizing during iterative deepening (`table.resize(nSearched * 2)` up to memory ceiling).
  - Replacement scheme: Depth-preferred with generation aging.

### 4.7 Time Management & Dynamic Allocation
Ported from Carbon Gomoku's timing heuristics:
* **Soft Limit:** During iterative deepening, before starting depth $D+1$, the engine estimates whether time will expire using:
  $$t_{\text{current}} + \text{TIMEOUT\_PREVENT} \times t_{\text{last\_depth}} \ge t_{\text{budget}}$$
  where $\text{TIMEOUT\_PREVENT} = 5$. If projected to exceed the budget, search stops and returns the best move from depth $D$.
* **Game Clock Allocation:** In `TimePerGame` mode, remaining time is distributed dynamically:
  $$t_{\text{move}} = \min(t_{\text{timeout\_turn}}, \frac{t_{\text{remaining}}}{\text{MATCH\_SPARE}}) - 30\text{ ms}$$
  where $\text{MATCH\_SPARE} = 7$.
* **Hard Interrupt:** Cooperative cancellation checks every 1000 nodes (`cnt = 1000`) checking elapsed milliseconds and cancellation tokens.

### 4.8 Computer Options & Settings Dialog (Matching Stello & Connect-4)
Configurable via `Game -> Settings...` dialog:
1. **AI Opponent Engine**:
   - **Carbon AI (Advanced Minimax):** Full Alpha-Beta engine with iterative deepening, TT, pattern bitboards, and quickWinSearch.
   - **Simple AI (Heuristic Baseline):** Fast non-searching pattern evaluator ported from Carbon's `AISimple`.
2. **Fixed Depth**:
   - Slider: 1 to 20 plies (default: 6 plies).
3. **Time per Move**:
   - Slider: 1 to 60 seconds (default: 5 seconds).
4. **Time per Game**:
   - Slider: 1 to 60 minutes (default: 5 minutes) with live chess clock countdown and refund on Undo.
5. **Transposition Table Size**:
   - Slider: $2^{16}$ ($65,536$) up to $2^{24}$ ($16,777,216$) entries (default: $2^{20} = 1,048,576$ entries).
6. **Rule Variant**:
   - Freestyle Gomoku (Default, 5+ wins)
   - Exact 5 (Overlines do not count)
   - Renju Mode (Black foul restrictions)

---

## 5. Iterative Implementation Plan

| Phase | Milestone | Deliverables | Status |
| :--- | :--- | :--- | :--- |
| **Phase 1** | **Core Domain & Rule Engine** | • 19x19 board representation & coordinate models<br>• Incremental 5-in-a-row detection in all 4 directions<br>• Win/loss/draw terminal condition verification<br>• Variant rules: Exact 5 and Renju foul detection (`checkForbid`)<br>• 100% test coverage on move edge cases and overlines | **PLANNED** |
| **Phase 1.5** | **Engine Documentation (`docs/brain`)** | • Create `docs/brain/` structure matching Stello & Connect-4<br>• `README.md` (The Gomoku brain on one page)<br>• `01-overview.md` (Architecture, life of a move)<br>• `02-board-and-coordinates.md` (19x19 grid, star points, indexing)<br>• `03-rules-and-patterns.md` (Win conditions, Exact 5, Renju fouls)<br>• `04-game-record.md` (GameSession, history, SGF format) | **PLANNED** |
| **Phase 2** | **Shared App Layer & WPF Desktop GUI** | • `Gomoku.App` shared ViewModels & game coordinator<br>• `Gomoku.Wpf` desktop application<br>• Responsive 19x19 wooden board with star points & stone rendering<br>• Click-to-place, hover preview, last move marker & winning line highlight<br>• Turn management, Undo/Redo, and sound effects | **PLANNED** |
| **Phase 3** | **AI Engines, Analysis & Game Records** | • Port `SimpleAiPlayer` from Carbon's `AISimple`<br>• Port `PatternEngine` (`pattern[4][2]`, `CONFIG`, `COUNT5`, `PRIOR`, `STATUS1`, `STATUS4`)<br>• Port `CarbonAiPlayer` with Alpha-Beta, Iterative Deepening & `quickWinSearch`<br>• Real-time **Analysis Pane** (Move, Depth, Score, Best Move, Nodes, Speed, Time)<br>• Full **Save & Load** system (SGF and coordinate format)<br>• Document chapters `05-evaluation-and-heuristics.md`, `06-move-ordering-and-candidates.md`, `07-search-and-quickwin.md` | **PLANNED** |
| **Phase 4** | **Computer Settings, Time Controls & Engine Polish** | • **Computer Settings Dialog** matching Stello & Connect-4 (Engine, Depth, Time per move, Time per game, TT size, Variant)<br>• **Chess clock / time management** (`MATCH_SPARE`, `TIMEOUT_PREVENT`, clock refund on undo)<br>• **Transposition Table** (Triple-hash Zobrist signatures, 80-bit keys, replacement policies)<br>• **Empirical benchmark runner** (`tools/Gomoku.Benchmark`) verifying node cutoffs and speedup<br>• Document chapters `08-transposition-table.md`, `09-time-control.md` | **PLANNED** |
| **Phase 5** | **Blazor WebAssembly Client & Deployment** | • `Gomoku.Web` project with .NET 10 WebAssembly AOT<br>• Responsive Web board UI inspired by Stello and Connect-4<br>• Integrated `/docs` viewer rendering `docs/brain` using Markdig<br>• CI/CD pipeline: `.github/workflows/azure-static-web-apps.yml`<br>• Automated deployment to Azure Static Web Apps | **PLANNED** |

---

## 6. Target Quality & Test Metrics

- **Platform:** .NET 10 (C# 13)
- **Compiler Warnings:** 0 warnings across all projects (Debug & Release builds)
- **Automated Unit Testing Target:** > 100 tests across core and app layers
  - `Gomoku.Core.Tests`: Board operations, 5-in-a-row detection in 4 directions, overline handling, Exact5 flag, Renju forbidden moves, pattern tables, candidate generator, quickWinSearch, Alpha-Beta minimax, TT consistency, SGF parser/serializer.
  - `Gomoku.App.Tests`: ViewModels, GameSession, Turn coordination, Clock refund on Undo, File I/O, Settings persistence.
- **Search Performance Target:** > 500,000 nodes/sec in .NET 10 Release builds, achieving deep tactical play (depth 6–10) within 1–3 seconds per move.