import fs from 'fs';

const filePath = 'docs/brain/01-overview.md';
let content = fs.readFileSync(filePath, 'utf8').replace(/\r\n/g, '\n');

// 1. Mermaid flowchart update: include Gomoku.Web.Engine
const oldFlowchart = `\`\`\`mermaid
flowchart LR
    Wpf["Gomoku.Wpf<br/>WPF Desktop App<br/>net10.0-windows"] --> Shared["Gomoku.App<br/>ViewModels &amp; Services<br/>net10.0"]
    Web["Gomoku.Web<br/>Blazor WebAssembly App<br/>net10.0"] --> Shared
    Shared --> Core["Gomoku.Core<br/>Domain &amp; Carbon Engine<br/>net10.0"]
    AppTests["Gomoku.App.Tests<br/>ViewModel &amp; Service Tests"] --> Shared
    AppTests --> Core
    CoreTests["Gomoku.Core.Tests<br/>Engine, Rules &amp; AI Tests"] --> Core
    Bench["Gomoku.Benchmark<br/>CLI Performance Tool<br/>net10.0"] --> Core
\`\`\``;

const newFlowchart = `\`\`\`mermaid
flowchart LR
    Wpf["Gomoku.Wpf<br/>WPF Desktop App<br/>net10.0-windows"] --> Shared["Gomoku.App<br/>ViewModels &amp; Services<br/>net10.0"]
    Web["Gomoku.Web<br/>Blazor WebAssembly App<br/>net10.0"] --> Shared
    WebEngine["Gomoku.Web.Engine<br/>AOT WASM Engine Runtime<br/>net10.0"] --> Core["Gomoku.Core<br/>Domain &amp; Carbon Engine<br/>net10.0"]
    Web -.->|"Launches Worker"| WebEngine
    Shared --> Core
    AppTests["Gomoku.App.Tests<br/>ViewModel &amp; Service Tests"] --> Shared
    AppTests --> Core
    CoreTests["Gomoku.Core.Tests<br/>Engine, Rules &amp; AI Tests"] --> Core
    Bench["Gomoku.Benchmark<br/>CLI Performance Tool<br/>net10.0"] --> Core
\`\`\``;

if (!content.includes(oldFlowchart)) {
  console.log('WARNING: oldFlowchart not found');
} else {
  content = content.replace(oldFlowchart, newFlowchart);
}

// 2. Core Engine Architecture class diagram: update to BitboardPatternBoard, CarbonBitboardAiPlayer, LockFreeTranspositionTable
const oldClassDiagram = `\`\`\`mermaid
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
\`\`\``;

const newClassDiagram = `\`\`\`mermaid
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

    class BitboardPatternBoard {
        +Cell[] Cells
        +uint[] Occupied
        +int BoardWidth
        +int BoardHeight
        +GomokuVariant Variant
        +PieceColor Who
        +ulong ZobristKey
        +Point UpperLeftCand
        +Point LowerRightCand
        +DilateRow(m, boardMask)$ uint
        +GetCandidateMask(by, dilatedRows, boardMask) uint
        +MakeMove(x, y)
        +MakePaddedMove(xp, yp)
        +UndoMove()
        +CheckForbid(x, y)
        +IsForbidden(x, y, player) bool
    }

    class Cell {
        +PieceColor Piece
        +byte Pattern00, Pattern01
        +byte Pattern10, Pattern11
        +byte Pattern20, Pattern21
        +byte Pattern30, Pattern31
        +byte Status1_00, Status1_01
        +byte Status4_0, Status4_1
        +Update1(k, exact5)
        +Update4()
        +Prior() short
    }

    class CarbonBitboardAiPlayer {
        +LockFreeTranspositionTable Table
        +GetMoveAsync(state, limits, legalMoves, progress, token) ValueTask~Move~
        -SearchLazySmp(state, limits, token, moveNowToken) Cand
        -MinimaxWorker(workerId, depth, alpha, beta, token) Cand
        -QuickWinSearch(pb) int
        -GenerateCand(pb, cnd, nCnd, ttBest)
        -Evaluate(pb) int
    }

    class SimpleAiPlayer {
        +GetMoveAsync(state, limits, legalMoves, progress, token) ValueTask~Move~
        -EvalCell(state, x, y, who) int
        -CountStonesIn5(state, startX, startY, dx, dy, who) int
    }

    class LockFreeTranspositionTable {
        +int Capacity
        +TryProbe(key, depth, eval, bound, bestMove) bool
        +Store(key, depth, eval, bound, bestMove)
        +NewSearch()
        +Clear()
    }

    BitboardPatternBoard *-- Cell : 1D Padded Array (Stride 128)
    CarbonBitboardAiPlayer *-- BitboardPatternBoard : per-worker board clones
    CarbonBitboardAiPlayer *-- LockFreeTranspositionTable : shared lock-free cache
    BoardState <.. CarbonBitboardAiPlayer : input position
    BoardState <.. SimpleAiPlayer : input position
\`\`\``;

if (!content.includes(oldClassDiagram)) {
  console.log('WARNING: oldClassDiagram not found');
} else {
  content = content.replace(oldClassDiagram, newClassDiagram);
}

// 3. Sequence diagram: update participants and loop depth
const oldSeq = `\`\`\`mermaid
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
\`\`\``;

const newSeq = `\`\`\`mermaid
sequenceDiagram
    autonumber
    actor Player as Human Player
    participant VM as MainViewModel (UI Thread)
    participant Worker as Task.Run / Web Worker
    participant AI as CarbonBitboardAiPlayer
    participant Book as OpeningBook
    participant PB as BitboardPatternBoard
    participant TT as LockFreeTranspositionTable
    participant Sound as ISoundService

    Player->>VM: Clicks Intersection (x, y)
    VM->>VM: Session.ApplyMove(move)
    VM->>Sound: PlaySoundAsync(StonePlaced)
    VM->>VM: UpdateUiState() (check Renju fouls, clocks, game over)
    
    alt Is AI Turn
        VM->>Worker: Dispatch GetMoveAsync(state, limits, legalMoves)
        Worker->>AI: SearchLazySmp(...)
        
        opt MoveCount == 0
            AI-->>VM: Play Geometric Center (H8)
        end

        opt MoveCount in [1..6] & Book Enabled
            AI->>Book: TryGetMove(state, out bookPoint)
            Book-->>AI: Found book move (Dihedral D4 match)
            AI-->>VM: Return Book Move
        end

        AI->>PB: Start() & Replay Placed Stones
        AI->>TT: NewSearch() (increment age)
        
        loop Iterative Deepening (Depth d = 1, 2, 3, 4, ...)
            AI->>AI: QuickWinSearch() (VCF tactical mate check)
            AI->>TT: TryProbe(zobristKey, out ttMove)
            AI->>AI: GenerateCand() (Chebyshev dilation + ONE_CAND)
            AI->>AI: MinimaxWorker(d, alpha, beta) across Lazy SMP threads
            AI-->>VM: Progress.Report(SearchAnalysis) (Depth, Score, NPS, PV)
            Note over VM: Updates live analysis card on UI thread
        end
        
        AI-->>Worker: Chosen Move
        Worker-->>VM: Chosen Move
        VM->>VM: Session.ApplyMove(bestMove)
        VM->>Sound: PlaySoundAsync(StonePlaced)
        VM->>VM: UpdateUiState()
    end
\`\`\``;

if (!content.includes(oldSeq)) {
  console.log('WARNING: oldSeq not found');
} else {
  content = content.replace(oldSeq, newSeq);
}

// Convert back to CRLF
fs.writeFileSync(filePath, content.replace(/\n/g, '\r\n'), 'utf8');
console.log('01-overview.md written with CRLF.');

