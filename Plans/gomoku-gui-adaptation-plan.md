# Implementation Plan: Gomoku GUI Adaptation to General Game GUI Specification

## Goal Description
The objective is to adapt the Gomoku Web GUI (`c:\Udvikling\Spil\Gomoku\src\Gomoku.Web` and its shared presentation layer `Gomoku.App`) to match the tournament-grade architecture and design specified in `general-game-gui-specification.md`, which is based on Checkers .NET (`c:\Udvikling\Spil\Checkers\src\Checkers.Web`).

### Decisions Incorporated From User Feedback:
1. **Undo & Redo Menu Items:** Completely removed from top menus (the `Edit` menu is eliminated; `Undo` and `Redo` will exist strictly as Quick Action buttons beneath the scoreboard, with `Ctrl+Z` / `Ctrl+Y` shortcuts preserved).
2. **Settings Shortcut:** No `F2` shortcut for the Settings dialog (Settings is accessed exclusively via `Game -> Settings...`).
3. **Board Orientation:** Kept strictly fixed (no board rotation when playing Black or White).
4. **Player Scorecards:** Layer 3 displays the large stone count (`@Vm.BlackPiecesCount` / `@Vm.WhitePiecesCount`), and Layer 4 displays `"Stones Placed"`.

---

## Specification Parity Overview

1. **Responsive Mobile Navigation Shell:** Mobile header (`☰`) and sliding drawer ($\le 768\text{ px}$) with backdrop and $\ge 44\text{ px}$ touch targets.
2. **Standard 4-Menu Desktop Hierarchy:** Clean 4-menu hierarchy (`File`, `Game`, `Options`, `Help`), removing the old `Edit` menu.
3. **Mid-Game Safeguards & Confirmations:** Confirmation prompts when abandoning a match in progress via `New Game` or switching game modes.
4. **Mid-Match Side Swapping (`Switch sides`):** Allowing the player to swap sides with the engine mid-game during their turn.
5. **Competitive Fair Play Tracking (`IsGameModified`):** Real-time tracking of match integrity that turns the `"STATUS"` header crimson red (`#E74C3C`) upon Undo, Switch Sides, mid-match settings adjustment, or continuation after timeout.
6. **Chess Clock & Timeout Continuation:** Turn-level time refund upon `Undo` and interactive continuation prompt (*"You ran out of time! Do you want to continue playing?"*) instead of immediate match termination.
7. **Scoreboard & Header Refinement:** 4-layer player card contract (incorporating digital clocks into Layer 1 header instead of bottom footer), right-aligned engine thinking limit badge in top status header (`5 min / game`, `5s / move`, `8 plies`), and normalized Quick Action buttons (`[Undo]`, `[Redo]`, `[Move Now]`).

Game-specific elements—principally the Gomoku SVG board (`Board.razor`), intersection placement, stone rendering, coordinate numbering, and game engine rules—remain strictly untouched.

---

## User Review Required

> [!IMPORTANT]
> **Menu Layout:**
> - `File`: `New Game` (`Ctrl+N`), `Open...` (`Ctrl+O`), `Save` (`Ctrl+S`), `Save As...`
> - `Game`: `vs Computer (Black)` (`●`), `vs Computer (White)` (`●`), `vs Human (Two players)` (`●`), separator, `Switch sides`, separator, `Settings...`
> - `Options`: `Analysis` (`Ctrl+A`), `Sound`, separator, `Coordinates`, `Ghost Stone`, `Highlight Last Move`, `Highlight Winning Line`
> - `Help`: `User guide` (`F1`), `Rules & Documentation` (`Shift+F1`), `About Gomoku .NET...`
>
> **Action Controls Row:**
> - Clean 3-button row: `[Undo]` `[Redo]` `[Move Now]`
> - `[New]` button removed from this row (accessed via `File -> New Game` / `Ctrl+N`).
> - `[Move Now]` rendered persistently in `vs Computer` mode, disabled when the AI is not calculating, avoiding button bouncing.

---

## Proposed Changes

### Layer 1: Core Presentation Logic (`Gomoku.App`)

#### [MODIFY] [`GameSettings.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.App/Models/GameSettings.cs)
- Add `Summary` property to generate badge text based on time control and AI engine:
  - If `AiEngine == AiEngineType.Simple`: `"Simple AI"`
  - If `Carbon`:
    - `FixedDepth`: `"{SearchDepth} plies"`
    - `TimePerMove`: `"{MoveTimeSeconds}s / move"`
    - `TimePerGame`: `"{GameTimeMinutes} min / game"`
- Add `VariantBadgeText` property (`"Freestyle"`, `"Standard"`, `"Renju"`).

#### [MODIFY] [`MainViewModel.cs`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.App/ViewModels/MainViewModel.cs)
- **Competitive Integrity & Modified State:**
  - Add `_isGameModified` observable property.
  - Add `IsStatusModified => IsVsComputer && IsGameModified`.
  - Add `_hasHumanContinuedOnTime` flag.
  - Reset `IsGameModified = false` upon new game initialization or file load.
  - Mark `IsGameModified = true` upon:
    - `Undo` in `vs Computer` mode.
    - `SwitchSides`.
    - Mid-game settings adjustment (if moves have been played and playing vs computer).
    - Accepting the continuation dialog after human clock timeout.
- **Side Swapping (`Switch sides`):**
  - Add `CanSwitchSides => IsVsComputer && IsHumanTurn() && !GameStatus.IsGameOver() && !IsThinking`.
  - Add `SwitchSides()`:
    - Toggles `Settings.Mode` between `GameMode.HumanVsAi` and `GameMode.AiVsHuman`.
    - Sets `IsGameModified = true`.
    - Updates label and turn properties.
    - If now AI turn, triggers AI search.
- **Restart Safeguards:**
  - Update `NewGameCommand` and `SetGameModeCommand` to check if a match is in progress with plies played (`!GameStatus.IsGameOver() && Session.MoveHistory.Count > 0`).
  - If active, prompt `_dialogService.ShowConfirmationAsync("Abandon Match", "A game is currently in progress. Abandon current match and start a new game?")` before resetting.
- **Clock Time Refund on Undo:**
  - Maintain `Dictionary<int, (TimeSpan Black, TimeSpan White)> _timeAtPly` snapshots when moves are recorded.
  - On `Undo()`, restore remaining time for both sides from the target ply.
- **Interactive Timeout Continuation Flow:**
  - In `HandleTimeout(PieceColor timedOutPlayer)`:
    - If `IsVsComputer` and the timed-out player is the human and `!_hasHumanContinuedOnTime`:
      - Prompt continuation dialog: `_dialogService.ShowConfirmationAsync("Time Expired", "You ran out of time!\n\nDo you want to continue playing?")`.
      - If accepted: set `_hasHumanContinuedOnTime = true`, `IsGameModified = true`, freeze clock at `0:00`, and resume play.
      - If rejected or if computer timed out: terminate game with loss on time and sound alert.
- **Badges & Header Properties:**
  - Add `IsGameSettingVisible => IsVsComputer`.
  - Add `SettingsBadgeText => Settings.Summary`.
  - Add `MoveCountText => $"{Session.MoveHistory.Count} moves"`.

---

### Layer 2: Web View & Styles (`Gomoku.Web`)

#### [MODIFY] [`Home.razor`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Web/Pages/Home.razor)
- **Top Menu Hierarchy:**
  - Restructure into 4 menus (`File`, `Game`, `Options`, `Help`):
    - `File`: `New Game` (`Ctrl+N`), `Open...` (`Ctrl+O`), `Save` (`Ctrl+S`), `Save As...`.
    - `Game`: `vs Computer (Black)` (`●`), `vs Computer (White)` (`●`), `vs Human (Two players)` (`●`), separator, `Switch sides`, separator, `Settings...`.
    - `Options`: `Analysis` (`Ctrl+A`), `Sound`, separator, `Coordinates`, `Ghost Stone`, `Highlight Last Move`, `Highlight Winning Line`.
    - `Help`: `User guide` (`F1`), `Rules & Documentation` (`Shift+F1`), `About Gomoku .NET...`.
  - Note: No Undo/Redo in menus (only on Quick Action buttons). No `F2` shortcut on Settings.
- **Mobile Header & Drawer ($\le 768\text{ px}$):**
  - Add `.mobile-nav-bar` with brand title `Gomoku` and hamburger button `☰`.
  - Add `.mobile-drawer` with drawer header (`Menu`, close `✕`), scrollable drawer sections, radio dots `●`, checkmarks `✓`, and backdrop overlay `.mobile-drawer-backdrop`.
- **Status Panel Header:**
  - Apply `class="@(Vm.IsStatusModified ? "status-modified" : null)"` to `<h2>STATUS</h2>`.
  - Render `<span class="mode-badge">@Vm.VariantBadgeText</span>` using stylesheet styles.
  - Render `@if (Vm.IsGameSettingVisible) { <span class="setting-text">@Vm.SettingsBadgeText</span> }`.
- **Dual-Card Scoreboard Contract:**
  - Reconstruct player cards to match Checkers:
    - Layer 1 (`.player-header`): `.player-title` with circular dot and player name on the left; `.player-clock` on the right (visible when `IsClockVisible`).
    - Layer 2 (`.player-role`): Human / Computer / Player 1 / Player 2.
    - Layer 3 (`.piece-count`): `@Vm.BlackPiecesCount` and `@Vm.WhitePiecesCount`.
    - Layer 4 (`.kings-count`): `"Stones Placed"`.
- **Quick Action Buttons:**
  - Clean 3-button row:
    ```razor
    <button class="btn btn-secondary" disabled="@(!Vm.CanUndo)" @onclick="() => RunAsync(Vm.UndoCommand)">Undo</button>
    <button class="btn btn-secondary" disabled="@(!Vm.CanRedo)" @onclick="() => RunAsync(Vm.RedoCommand)">Redo</button>
    @if (Vm.IsVsComputer)
    {
        <button class="btn btn-warning" disabled="@(!Vm.IsThinking)" @onclick="() => RunAsync(Vm.MoveNowCommand)" title="Play current best move immediately (Ctrl+M)">Move Now</button>
    }
    ```

#### [MODIFY] [`app.css`](file:///c:/Udvikling/Spil/Gomoku/src/Gomoku.Web/wwwroot/css/app.css)
- Add full mobile navigation drawer rules matching Checkers:
  - `.mobile-nav-bar`, `.desktop-menu-bar`, `.app-brand`, `.hamburger-btn`.
  - `.mobile-drawer`, `.mobile-drawer.open`, `.mobile-drawer-header`, `.mobile-drawer-title`, `.mobile-drawer-close`, `.mobile-drawer-body`.
  - `.mobile-menu-section`, `.mobile-section-header`, `.mobile-section-items`, `.mobile-menu-item`, `.mobile-drawer-backdrop`.
- Add status modified crimson styling:
  - `.panel-header h2.status-modified { color: #E74C3C; }`
- Add badge and settings text styling:
  - `.mode-badge { background: #16A085; color: #FFFFFF; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; }`
  - `.setting-text { font-size: 0.78rem; font-weight: 600; color: #64748B; margin-left: auto; }`
- Add player header and clock styling:
  - `.player-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.25rem; }`
  - `.player-clock { font-weight: 700; font-size: 0.85rem; color: #475569; font-variant-numeric: tabular-nums; }`
  - `.score-card.active-turn .player-clock { color: #1E3A8A; }`
- Add `.btn-warning:disabled` state styling for inactive Move Now button.

---

## Verification Plan

### Automated Tests
Run full solution test suite ensuring no regressions in Gomoku core rules, pattern indexing, AI searches, or ViewModel commands:
```powershell
dotnet test C:\Udvikling\Spil\Gomoku\Gomoku.slnx
```

Add/update unit tests in `Gomoku.App.Tests`:
1. `IsGameModified` activates when `Undo` is executed in `HumanVsAi`.
2. `IsGameModified` activates when `SwitchSides` is executed.
3. `SwitchSides` changes mode between `HumanVsAi` and `AiVsHuman` and triggers AI move if appropriate.
4. `Undo` restores clock times from `_timeAtPly`.
5. `SetGameMode` and `NewGame` safeguards operate properly.

### Manual Verification
1. **Desktop Menu Bar:**
   - Verify 4 menus (`File`, `Game`, `Options`, `Help`).
   - Verify Undo/Redo do NOT exist in any menu, only on the Quick Action buttons (and `Ctrl+Z` / `Ctrl+Y`).
   - Verify `Settings...` is in `Game` menu and has no F2 shortcut.
2. **Mobile Drawer ($\le 768\text{ px}$):**
   - Resize browser to mobile width ($< 768\text{ px}$).
   - Verify desktop menu hides, brand title `Gomoku` and `☰` appear.
   - Click `☰`: verify smooth slide-out drawer with dark backdrop.
   - Click an item or backdrop: verify drawer closes cleanly.
3. **Scoreboard & Modified Status:**
   - Start game vs Computer. Note `"STATUS"` is slate gray.
   - Place a stone, then click `Undo`. Verify `"STATUS"` turns bold crimson red (`#E74C3C`).
   - Verify `[Freestyle]` variant badge and `[5s / move]` limit badge render cleanly on top row.
   - Verify player cards show clocks next to player names on Layer 1, and Layer 4 says `"Stones Placed"`.
4. **Switch Sides:**
   - During human's turn, select `Game -> Switch sides`.
   - Verify player mode flips, `"STATUS"` turns red, and the computer immediately begins calculating for that side. Board orientation does not flip.
5. **Timeout Continuation:**
   - Set Time per Game to 1 minute in Settings.
   - Let player clock reach `0:00`.
   - Verify dialog appears: *"You ran out of time! Do you want to continue playing?"*.
   - Click Yes: verify clock freezes at `0:00`, `"STATUS"` turns red, and the game continues.
