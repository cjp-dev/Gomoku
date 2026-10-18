# 13 – UI clients

[Back to the index](README.md)

## In short

Gomoku .NET delivers a modern, high-fidelity user experience across two distinct presentation targets:
1. **WPF Desktop Client (`Gomoku.Wpf`):** A Windows desktop application using DirectX hardware acceleration, sub-pixel text rendering, and native OS dialog integration.
2. **Blazor WebAssembly Client (`Gomoku.Web`):** An in-browser client compiled via WebAssembly Ahead-of-Time (AOT), running 100% client-side with zero server latency, complete with a responsive SVG game board and embedded architectural documentation viewer.

Both clients share the exact same view models, game engine, and procedural audio synthesis from `Gomoku.App` and `Gomoku.Core`.

![Gomoku .NET User Interface](images/gui.png)

---

## Presentation architecture & UI layout

```mermaid
graph TD
    subgraph UI Wireframe
        TopBar["Menu Bar / Navigation<br/>Game • View • Options • Help"]
        Scoreboard["Dual-Card Scoreboard<br/>Black Card • Turn/Status Banner • White Card"]
        Board["Interactive Board Canvas<br/>Kaya Wood Texture • Coordinates • Star Points • Ghost Stones"]
        Analysis["AI Analysis Panel (Toggleable)<br/>Depth • Score • Best Move • Nodes • Speed • Time • Status"]
        DocViewer["Docs Subsystem (Web Only)<br/>Interactive Markdown Engine • TOC Pager • KaTeX Math"]
    end

    TopBar --> Scoreboard
    Scoreboard --> Board
    Scoreboard --> Analysis
    TopBar --> DocViewer
```

---

## Visual board representation (Kaya wood & stones)

Both desktop and web clients render the board with authentic Japanese Go/Renju aesthetic styling:

### 1. Kaya Wood Texture & Grid Lines
The board surface uses a multi-stop golden-amber gradient simulating aged Japanese Hon-Kaya wood:
```xml
<linearGradient id="wood-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#E8C99B" />
    <stop offset="50%" stop-color="#DCB985" />
    <stop offset="100%" stop-color="#D2A673" />
</linearGradient>
```
Grid lines are drawn with double-weight borders ($2.0\text{ px}$) around the perimeter and crisp interior lines ($1.2\text{ px}$).

### 2. Star Points (Hoshi)
Star points (traditional black alignment dots, $r = 4\text{ px}$) are placed according to the active board size:
- **$15 \times 15$ Board:** 5 star points at `(3, 3), (11, 3), (7, 7), (3, 11), (11, 11)` (D4, L4, H8, D12, L12).
- **$19 \times 19$ Board:** 9 star points at traditional Go intersections (columns 4, 10, 16).
- **$20 \times 20$ Board:** 4 star points at `(4, 4), (15, 4), (4, 15), (15, 15)`.

### 3. Slate and Clamshell 3D Stones
Stones are rendered with realistic lighting gradients and drop shadows:
- **Black Stones:** Slate appearance with off-center specular reflection at $(cx = 32\%, cy = 32\%)$, transitioning from charcoal gray (`#555555`) to deep midnight black (`#080808`).
- **White Stones:** Mexican clamshell appearance with subtle off-white spherical gradient from pure white (`#FFFFFF`) to pearl cream (`#D0D0D0`).
- **Drop Shadow:** SVG `feDropShadow` filter ($dx = 2, dy = 3, \sigma = 3$) giving the physical illusion of stones resting on wood.

---

## Interactive board features

### 1. Hover Ghost Stones
When the player hovers the cursor over an empty intersection:
- If the intersection is a legal move, a semi-transparent ghost stone ($50\%$ opacity) of the current player's color is displayed, providing visual confirmation of placement alignment.
- When the mouse leaves the intersection or clicks to place, the ghost stone vanishes.

### 2. Renju Real-Time Foul Markers
Under Renju rules, Black is forbidden from creating Overlines, Double-Threes, or Double-Fours. When Black is to move:
- The UI scans the board using `PatternBoard.CheckForbid()`.
- Forbidden intersections are marked with a distinct red diagonal cross ($\times$).
- Clicking a marked intersection triggers the procedural illegal-move audio buzzer and displays an explanatory tooltip, preventing inadvertent fouls.

### 3. Last Move & Winning Line Highlights
- **Last Move Marker:** A contrasting circular ring or center dot indicates the most recently played stone.
- **Winning Line Highlight:** When a five-in-a-row is completed, the winning stones are encircled in glowing amber/gold rings, and the board enters terminal display mode.

---

## Blazor WebAssembly client (`Gomoku.Web`)

The web client runs entirely in the browser using WebAssembly Ahead-of-Time (AOT) compilation:
- **High Computational Throughput:** WebAssembly AOT compiles C# IL directly to native WebAssembly bytecode, achieving $> 70\%$ of native C# desktop performance.
- **Zero Server Latency:** AI calculations, tactical VCF searches, and audio generation execute directly on the user's CPU without making HTTP API roundtrips.
- **Responsive Layout:**
  - **Desktop View:** Side-by-side layout with $640 \times 640$ SVG board on the left and full telemetry dashboard on the right.
  - **Mobile View:** Vertically stacked layout with touch-optimized target intersections ($> 32\text{ px}$ hit zones).

### Integrated Brain Documentation Viewer (`Docs.razor`)
The web application hosts the entire `docs/brain` repository directly at `/docs`:
- Dynamic Markdown rendering via Markdig.
- Mathematical equation rendering via KaTeX.
- Automatic table of contents generation and inter-chapter pager navigation.
- Offline accessibility via Service Worker and Progressive Web App (PWA) manifest.

---

## Data structures

| Component | Host | Technology | Role |
|---|---|---|---|
| `MainWindow.xaml` | `Gomoku.Wpf` | WPF XAML / DirectX | Hardware-accelerated desktop view |
| `Board.razor` | `Gomoku.Web` | Blazor WASM / SVG | Interactive web game board component |
| `DialogHost.razor` | `Gomoku.Web` | Blazor Components | Modal settings, new game, and about dialogs |
| `Docs.razor` | `Gomoku.Web` | Markdig / KaTeX | Interactive architecture documentation viewer |
| `BrainDocs.cs` | `Gomoku.Web` | C# Service | Markdown loader, URL rewriter, and chapter parser |

---

## Where in the code

| File | Member / Method | Description |
|---|---|---|
| [MainWindow.xaml](../../src/Gomoku.Wpf/MainWindow.xaml) | `MainWindow` | WPF primary desktop user interface and canvas bindings |
| [Board.razor](../../src/Gomoku.Web/Components/Board.razor) | `Board` | Scalable SVG board with wood gradients, stones, and star points |
| [DialogHost.razor](../../src/Gomoku.Web/Components/DialogHost.razor) | `DialogHost` | Modal dialog manager for game settings and rules |
| [Docs.razor](../../src/Gomoku.Web/Pages/Docs.razor) | `Docs` | Live browser manual reader with KaTeX math rendering |
| [BrainDocs.cs](../../src/Gomoku.Web/Services/BrainDocs.cs) | `BrainDocs` | Document discovery service matching chapter regexes |

---

## Tests

- [ViewModelTests.cs](../../tests/Gomoku.App.Tests/ViewModelTests.cs):
  - Star points layout test: verifies 15x15 has 5 star points and 19x19 has 9 star points.
  - Ghost stone reactivity: verifies ghost stone matches active player color.
  - Move numbering toggle: verifies coordinate text visibility binding.

---

## Further reading

- W3C Scalable Vector Graphics (SVG) 2.0 Specification.
- Microsoft Docs: *Blazor WebAssembly Ahead-of-Time (AOT) Compilation*.
- Nielsen, J. (1994). *Usability Engineering*. Morgan Kaufmann (Heuristics for board game UI design).

---

Previous: [12 App architecture and audio](12-app-architecture-and-audio.md) · Next: [14 GUI and settings](14-gui-and-settings.md)

