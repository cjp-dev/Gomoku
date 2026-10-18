# Specification: Modernizing Gomoku GUI (Matching Checkers)

## 1. Overview & Objective
The goal is to modernize the user interface of Gomoku .NET so that its visual design, layout architecture, menu system, and dialogs match the clean, polished light theme of **Checkers** (`c:\Udvikling\Spil\Checkers\src\Checkers.Web`).

The implementation followed a two-phase roadmap (both complete):
- **Phase 1 (Complete)**: Modernized `Gomoku.Web` to match `Checkers.Web`.
- **Phase 2 (Complete)**: Aligned `Gomoku.Wpf` with the same menu hierarchy, dual-card scoreboard structure, dialogs, and light theme.

The Gomoku board itself (wood gradient SVG / canvas, stone rendering, star points, coordinates, and intersection interaction handlers) **remains exactly as-is**.

---

## 2. Visual Theme & Palette

The user interface will transition from the current dark theme to the clean light theme used in Checkers:

| UI Element | Color / Value | Purpose |
| :--- | :--- | :--- |
| **Page Background** | `#F0F2F5` | Light slate-gray application canvas |
| **Primary Text** | `#2C3E50` / `#1E293B` | High-contrast dark charcoal text |
| **Secondary / Muted Text** | `#64748B` / `#718096` | Labels, metrics, and captions |
| **Panel / Card Background** | `#FFFFFF` | White raised cards with `box-shadow: 0 2px 6px rgba(0,0,0,0.06)` and `border-radius: 8px` |
| **Menubar Background** | `#2C3E50` | Slate dark top bar with crisp white text (`#FFFFFF`) |
| **Menubar Hover / Active** | `#34495E` | Subtle highlight on hovered top-level menu |
| **Menu Dropdown Popups** | `#FFFFFF` | Clean white dropdowns with `#D1D5DB` border and soft shadow |
| **Menu Dropdown Hover** | `#F1F5F9` | Hovered item background |
| **Accent / Primary Action** | `#3498DB` | Primary buttons, active turn borders, analysis accent |
| **Success / Variant Badge** | `#16A085` | Rules badge (Freestyle, Renju, Standard, Caro) |
| **Timer / Clock Highlight** | `#E67E22` | Clock badge and remaining time |
| **Borders & Dividers** | `#E2E8F0` | Subtle, clean separation borders |

---

## 3. Overall Page Layout

The main page (`Home.razor`) will follow the Checkers layout structure:

```
+---------------------------------------------------------------------------------------+
| Menubar: [File] [Edit] [Game] [Options] [Help]                                        |
+---------------------------------------------------------------------------------------+
| Main Content Area (max-width: 1300px, centered):                                      |
|                                                                                       |
|  +------------------------------+  +-----------------------------------------------+  |
|  |                              |  | STATUS PANEL                                  |  |
|  |                              |  | - Status headline & thinking progress bar     |  |
|  |                              |  | - Player Scoreboard:                          |  |
|  |                              |  |   [ Black: Human / 0 stones / 05:00 ] (Active)|  |
|  |                              |  |   [ White: Computer / 0 stones / 05:00 ]      |  |
|  |                              |  | - Quick Action Buttons: [New] [Undo] [Redo]   |  |
|  |      SVG GOMOKU BOARD        |  +-----------------------------------------------+  |
|  |     (15x15 / 19x19 / 20x20)  |  | ANALYSIS PANEL (Collapsible / Toggleable)     |  |
|  |     (Unchanged Wood SVG)     |  | - Depth, Score, Best Move, Nodes, Speed, Time |  |
|  |                              |  +-----------------------------------------------+  |
|  |                              |  | MOVE HISTORY PANEL                            |  |
|  |                              |  | - Scrollable ordered list: 1. H8 G7           |  |
|  +------------------------------+  +-----------------------------------------------+  |
+---------------------------------------------------------------------------------------+
```

---

## 4. Menu Structure & Shortcuts

The current top button bar will be replaced by a desktop-grade dropdown MenuBar matching `Checkers.Web`:

### 4.1 Menu Hierarchy

1. **File**
   - **New Game** (`Ctrl+N`)
   - **Open SGF / Record...** (`Ctrl+O`)
   - **Save SGF** (`Ctrl+S`)
   - **Save As...**

2. **Edit**
   - **Undo** (`Ctrl+Z`) – Disabled when history is empty or AI is thinking
   - **Redo** (`Ctrl+Y`) – Disabled when at latest move or AI is thinking

3. **Game**
   - **vs Computer (You play Black)** (`●` Radio)
   - **vs Computer (You play White)** (`●` Radio)
   - **vs Human (Two players)** (`●` Radio)
   - *--- (Separator) ---*
   - **Settings...**

4. **Options**
   - **Analysis Panel** (`✓` Checkbox, `Ctrl+A`)
   - **Sound Effects** (`✓` Checkbox)
   - **Coordinate Labels** (`✓` Checkbox)
   - **Ghost Stone on Hover** (`✓` Checkbox)

5. **Help**
   - **Rules & Documentation** (Opens `/docs` in new tab or route)
   - **About Gomoku .NET...**

### 4.2 Menu Features
- **Hover-to-switch**: Opening one menu lets you hover across other menus to view them without clicking again.
- **Backdrop click**: Clicking outside any menu closes it immediately.
- **Keyboard shortcuts**: Handled via JavaScript keyboard listener matching `checkers.js` (`Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+Z`, `Ctrl+Y`, `Ctrl+A`).

---

## 5. Side Panels & Scoreboard

### 5.1 Game Status Panel (`.status-panel`)
- **Panel Header**:
  - `STATUS` title (`font-size: 0.85rem; font-weight: 700; color: #718096;`)
  - Variant Badge (`#16A085`, e.g. "Freestyle", "Renju", "Standard", "Caro")
  - Clock Badge (active countdown or game clock)
- **Status Message**:
  - Main status headline (e.g. "Black to move (Move 1)", "White is thinking...")
  - Animated smooth thinking bar (`.thinking-bar` with looping `.thinking-progress`)
- **Player Scoreboard (`.pieces-scoreboard`)**:
  - Two side-by-side cards (Black & White):
    - Stone dot indicator (`black-dot` / `white-dot`)
    - Player name ("Black" / "White")
    - Role subtitle ("Human" / "Computer")
    - Stones placed count on board (e.g. "0 stones")
    - Remaining clock time (e.g., "05:00")
    - **Active turn styling**: The player whose turn it is receives a blue border `#3498DB` and soft blue background `#EFF6FF`.
- **Quick Action Buttons (`.action-buttons`)**:
  - Grid of 3 buttons: **New** (primary blue `#3498DB`), **Undo** (secondary gray), **Redo** (secondary gray).

### 5.2 AI Analysis Panel (`.analysis-panel`)
- Left blue border accent (`border-left: 4px solid #3498DB`)
- Header: `ANALYSIS`
- Two-column grid (`<dl class="analysis-grid">`):
  - **Move**: Current ply
  - **Depth**: Current search depth
  - **Score**: Numerical evaluation or win indicator
  - **Best Move**: Projected optimal move (e.g. `H8`)
  - **Nodes**: Total nodes searched
  - **Speed**: Nodes per second (NPS)
  - **Time**: Elapsed search duration
- Toggleable via **Options -> Analysis Panel** (`Ctrl+A`).

### 5.3 Move History Panel (`.history-panel`)
- Header: `MOVE HISTORY (#moves)`
- Scrollable list with structured ordered notation (`1. H8 G7`).

---

## 6. Settings Dialog

The Settings Dialog is hosted in `DialogHost.razor` using clean semantic fieldsets with legends (matching `Checkers.Web`):

1. **Fieldset 1: Board Size & Rules Variant**
   - **Board Size**: Radio buttons or segmented selector:
     - 15 × 15 (Standard Gomoku / Renju)
     - 19 × 19 (Go board standard)
     - 20 × 20 (Freestyle / Gomocup standard)
   - **Rule Variant**: Radio options:
     - Freestyle (5+ in a row wins, overlines permitted)
     - Standard (Exact 5 wins, overline does not count)
     - Renju (Black forbidden moves: 3x3, 4x4, overlines; White unrestricted)
     - Caro (Exact 5; lines blocked at both ends do not win)

2. **Fieldset 2: AI Engine & Thinking Limits**
   - **Engine**: Carbon AI (Advanced Alpha-Beta + Threat VCF) vs Simple AI
   - **Opening Book**: Checkbox for Carbon Opening Book (67 symmetry patterns)
   - **Search Mode**: Radio options with range sliders:
     - Fixed Depth (1 – 14 plies)
     - Time per move (1 – 20 seconds)
     - Time per game (1 – 30 minutes)

3. **Fieldset 3: Transposition Table**
   - Range slider for transposition table entries ($2^{18}$ to $2^{22}$ entries, displaying equivalent memory, e.g. "1,048,576 entries (16 MB)").

4. **Fieldset 4: Visual & Sound Preferences**
   - Sound Effects (toggle)
   - Coordinate Labels (toggle)
   - Ghost Stone on Hover (toggle)
   - Highlight Last Move & Winning Line (toggle)

- **Dialog Actions**: `Cancel` (secondary) and `OK` (default primary blue).

---

## 7. About Box

Modelled after Checkers' picture-dialog:
- **Left Column**: Author portrait (`images/claus-pedersen.jpg`) in a rounded frame with caption:
  - *Claus Pedersen*
  - *Author & Developer*
- **Right Column / Body**:
  - Title: **Gomoku .NET**
  - Subtitle: **Version 1.0.0**
  - Author and Year: Developed by **Claus Pedersen** (2026)
  - Description: High-performance Gomoku, Renju, and Caro engine featuring bitboards, Alpha-Beta minimax, transposition tables, and tactical threat-space VCF search.
  - Credits: Engine inspiration and algorithms adapted from Michał Czardybon (Carbon Gomoku).
- **Dialog Button**: Single centered `OK` button (`btn-primary`).

---

## 8. Technical Architecture & File Plan

### Phase 1: Gomoku.Web

| File | Description of Changes |
| :--- | :--- |
| `src/Gomoku.Web/wwwroot/css/app.css` | Replace dark theme CSS with Checkers light theme styles (menubar, dropdowns, cards, panels, scoreboard, dialogs, buttons, thinking bar, responsive layout). |
| `src/Gomoku.Web/Pages/Home.razor` | Update DOM to use `<nav class="menubar">`, `<main class="content">`, `.side-panels`, scoreboard cards, and action buttons. Add shortcut listener hook. |
| `src/Gomoku.Web/Components/DialogHost.razor` | Restructure to match Checkers DialogHost (`.dialog`, `.dialog-with-picture`, `.settings` fieldsets, keyboard focus on Enter/Escape). |
| `src/Gomoku.Web/Services/BrowserDialogService.cs` | Adopt the `DialogRequest` model for clean modal coordination and synchronous/async dialogs. |
| `src/Gomoku.Web/wwwroot/js/gomoku.js` | Add keyboard shortcut listener (`registerShortcuts`) for `Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+Z`, `Ctrl+Y`, `Ctrl+A`. |
| `src/Gomoku.Web/Components/Board.razor` | Preserved as-is (with culture-invariant SVG rendering). |

### Phase 2: Gomoku.Wpf (Complete)

| File | Description of Changes |
| :--- | :--- |
| `src/Gomoku.Wpf/MainWindow.xaml` | Aligned WPF native menu items to match (File, Edit, Game with 3 radio modes, Options, Help). |
| `src/Gomoku.Wpf/MainWindow.xaml` | Updated status sidebar to feature the symmetric dual-card scoreboard (Black / White cards with active turn border). |
| `src/Gomoku.Wpf/MainWindow.xaml.cs` | Added `OnDocsClicked` handler. |
| `src/Gomoku.Wpf/Views/SettingsWindow.xaml` | Grouped settings into the 4 semantic groupboxes matching Web. |
| `src/Gomoku.Wpf/Views/AboutWindow.xaml` | Aligned portrait layout and typography to match the 2-column picture format. |
| `src/Gomoku.App/ViewModels/MainViewModel.cs` | Added `SetGameModeCommand`, `IsBlackActiveTurn`, `IsWhiteActiveTurn`, and updated Computer label. |
| `src/Gomoku.App/ViewModels/SettingsViewModel.cs` | Added radio button and transposition table capacity binding helpers. |
