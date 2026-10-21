# Stello Web Architecture Implementation Plan

## Goal & Overview
You are tasked with upgrading the **Stello** application (`C:\Udvikling\Spil\Stello`) to implement the architectural recommendations from the Web Architecture Review.

### Summary of Changes to Implement
1. **Non-Destructive Web Worker Cancellation & Heartbeat Telemetry (`Stello.Engine` & `Stello.Web`)**:
   - **Live Heartbeats During Deep Single-Move Searches (`Stello.Engine/SearchEngine.cs`)**: Currently, `SearchEngine.Report(...)` is only called *after* a root move completes in `SearchRoot` and `SolveRoot`. During deep PVS re-searches or 18–22 empty endgame solves where a single root move can take several seconds, the Analysis panel freezes. Update `CheckAbort()` (which runs every 1,024 nodes) to emit a progress report every `100ms` for the currently active root move.
   - **Cooperative Worker Cancellation (`Stello.Web`)**: Currently, clicking **Move Now** (`Ctrl+M`), **Back** (`Undo`), or **New Game** while the computer is thinking calls `worker.terminate()`, destroying the 16 MB Midgame and Endgame Transposition Tables (`_midgameTable` and `_endgameTable`), losing the exact internal best move, and incurring a ~150–400ms worker cold reboot.
   - Add a 4-byte `SharedArrayBuffer` signal channel (`0 = running`, `1 = moveNow`, `2 = cancel`) between `engine-client.js` and `engine-worker.js` (with `wwwroot/staticwebapp.config.json` COOP/COEP headers for Azure Static Web Apps) polled by `EngineWorker.ProgressPoster` via `[JSImport("pollSignal", "engine-worker")]`.
   - When `moveNowToken` or `cancellationToken` fires in `WebEngineHost.ChooseMoveAsync`, signal the worker cooperatively and await up to `300ms` for a clean return—preserving the 16 MB Transposition Tables and returning the exact internal best move—while keeping `Restart(client)` (`worker.terminate()`) as a fallback if `SharedArrayBuffer` is unavailable or the timeout expires.
2. **Decoupled `<AnalysisPanel>` Rendering (`Stello.Web`)**:
   - Extract the Analysis `<aside>` from `Pages/Home.razor` into `Components/AnalysisPanel.razor` with its own `_renderPending` batching, and remove `Vm.Analysis.PropertyChanged += OnChanged;` from `Home.razor`.
   - This eliminates the 10 Hz `Board.OnParametersSet()` 64-square diff loop that currently runs on every analysis telemetry update while the AI is thinking.
3. **Responsive Layout Fixes for Mobile Landscape & Portrait (`Stello.Web/wwwroot/css/app.css`)**:
   - Replace `height: 100vh` on `.app` with `height: 100vh; height: 100dvh;` so mobile browser address bars do not clip the layout.
   - Fix the **Mobile Landscape Trap**: Currently `@media (max-width: 700px)` forces `flex-direction: column` and `width: min(100cqw, 100cqh)` even on landscape phones (e.g. `667px × 375px`), causing the board to consume 100% of the height and pushing the side panels completely off-screen. Use orientation/aspect-ratio aware media queries so landscape phones stay side-by-side (`flex-direction: row`), and cap portrait board height (`width: min(100cqw, 64cqh)`) so the Status panel and action buttons remain visible above the fold without scrolling.
4. **On-Screen Touch Action Buttons & Visual Thinking Bar (`Stello.Web/Pages/Home.razor` & `app.css`)**:
   - Add touch-friendly quick-action buttons (`New`, `Back`, `Forward`, and `Move Now` while `Vm.IsThinking` is true) and an animated `.thinking-bar` progress indicator inside the `Status` panel so mobile and tablet users do not have to open top dropdown menus for primary game actions.

> **CRITICAL RULE**: Do NOT break any existing unit tests in `Stello.Net/Stello.Engine.Tests` or `Stello.Net/Stello.Net.Tests`, and do NOT break the WPF app (`Stello.Net/Stello.Net`). Run `dotnet build Stello.Net/Stello.Net.slnx` and `dotnet test Stello.Net/Stello.Net.slnx` after each phase.

---

## Phase 1: Periodic Heartbeat Telemetry in `SearchEngine` (`Stello.Net/Stello.Engine`)

### 1.1 Update `Stello.Net/Stello.Engine/SearchEngine.cs`
1. Add fields to track the active root search state and last heartbeat timestamp (around line 50):
   ```csharp
   private long _lastHeartbeatMs;
   private int _reportDepth;
   private int _reportMove = -1;
   private int _reportBestMove = -1;
   private int _reportScore;
   private ScoreKind _reportKind;
   ```
2. In `Search(...)` (around line 90, where `_clock.Restart();` is called), reset them:
   ```csharp
   _lastHeartbeatMs = 0;
   _reportMove = -1;
   ```
3. In `SearchRoot` (around line 180, inside `for (int i = 0; i < rootMoves.Length; i++)` right before searching `move`):
   ```csharp
   _reportDepth = look + 1;
   _reportMove = move;
   _reportBestMove = bestMove >= 0 ? bestMove : best.Move;
   _reportScore = bestMove >= 0 ? bestScore : best.Score;
   _reportKind = ScoreKind.Heuristic;
   ```
4. In `SolveRoot` (around line 358, inside `for (int i = 0; i < rootMoves.Length; i++)` right before calling `Solve(...)`):
   ```csharp
   _reportDepth = empties;
   _reportMove = move;
   _reportBestMove = bestMove >= 0 ? bestMove : rootMoves[0];
   _reportScore = bestMove >= 0 ? bestScore : 0;
   _reportKind = kind;
   ```
5. In `Report(...)` (around line 709), update `_lastHeartbeatMs = _clock.ElapsedMilliseconds;` so heartbeats don't fire immediately after a completed root-move report:
   ```csharp
   private void Report(int depth, int move, int bestMove, int score, ScoreKind kind)
   {
       _lastHeartbeatMs = _clock.ElapsedMilliseconds;
       _progress?.Report(new SearchInfo(
           depth,
           new Square(move),
           bestMove >= 0 ? new Square(bestMove) : null,
           score,
           kind,
           _nodes,
           _evaluations,
           _clock.Elapsed));
   }
   ```
6. In `CheckAbort()` (around line 700, which is called every 1,024 nodes), emit a heartbeat report if `>= 100ms` has elapsed before checking `_cancel` and `_moveNow`:
   ```csharp
   private void CheckAbort()
   {
       if (_progress is not null && _reportMove >= 0 && _clock.ElapsedMilliseconds - _lastHeartbeatMs >= 100)
       {
           Report(_reportDepth, _reportMove, _reportBestMove, _reportScore, _reportKind);
       }

       _cancel.ThrowIfCancellationRequested();
       if (_moveNow.IsCancellationRequested || _clock.ElapsedMilliseconds >= _hardLimitMs)
       {
           throw new SearchAbortedException();
       }
   }
   ```
7. Run `dotnet test Stello.Net/Stello.Engine.Tests/Stello.Engine.Tests.csproj` and verify all engine tests pass.

---

## Phase 2: Non-Destructive Worker Cancellation (`Stello.Net/Stello.Web`)

### 2.1 Create `Stello.Net/Stello.Web/wwwroot/staticwebapp.config.json`
Create `staticwebapp.config.json` so Azure Static Web Apps serves Cross-Origin Isolation headers (enabling `SharedArrayBuffer` in browsers):
```json
{
  "globalHeaders": {
    "Cross-Origin-Opener-Policy": "same-origin",
    "Cross-Origin-Embedder-Policy": "require-corp"
  }
}
```

### 2.2 Update `Stello.Net/Stello.Web/wwwroot/js/engine-worker.js`
Add support for receiving an optional `signalBuffer` (`SharedArrayBuffer`) and exposing `pollSignal` to .NET via `setModuleImports`:
```javascript
// Web Worker that runs the engine in its own .NET runtime, loaded from the app's _framework folder.
// The page passes the (fingerprinted) URL of dotnet.js in the query string.
const dotnetUrl = new URL(self.location.href).searchParams.get('dotnet');
let signalView = null;

const started = (async () => {
    const { dotnet } = await import(dotnetUrl);
    const runtime = await dotnet.create();
    runtime.setModuleImports('engine-worker', {
        postProgress: json => self.postMessage({ type: 'progress', json }),
        pollSignal: () => signalView ? Atomics.load(signalView, 0) : 0,
    });
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    return exports.Stello.Web.Worker.EngineWorker;
})();

self.addEventListener('message', async e => {
    const { id, command, args, signalBuffer } = e.data;
    if (signalBuffer) {
        signalView = new Int32Array(signalBuffer);
    }
    try {
        const engine = await started;
        let result;
        switch (command) {
            case 'init': result = engine.Init(...args); break;
            case 'chooseMove': result = engine.ChooseMove(...args); break;
            case 'resetBookTracker': result = engine.ResetBookTracker(); break;
            case 'addGame': result = engine.AddGame(...args); break;
            case 'getBook': result = engine.GetBook(); break;
            default: throw new Error(`Unknown engine command: ${command}`);
        }
        self.postMessage({ type: 'response', id, result });
    } catch (error) {
        self.postMessage({ type: 'response', id, error: error?.message ?? String(error) });
    }
});
```

### 2.3 Update `Stello.Net/Stello.Web/wwwroot/js/engine-client.js`
Allocate a 4-byte `SharedArrayBuffer` when `typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated` is true, pass it with `init`, reset it to `0` before each `post`, and export `signal(code)` (`1 = moveNow`, `2 = cancel`):
```javascript
const canShareMemory = typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated;
const signalBuffer = canShareMemory ? new SharedArrayBuffer(4) : null;
const signalView = signalBuffer ? new Int32Array(signalBuffer) : null;
```
- In `post(command, args, includeBuffer = false)`:
  - If `signalView` is non-null, call `Atomics.store(signalView, 0, 0);`.
  - Include `signalBuffer` in `worker.postMessage` when `includeBuffer && signalBuffer` is true.
- In `start(progressTarget, book, hashBits)`:
  - Call `return post('init', [book, hashBits], true);`.
- Add:
  ```javascript
  // Signals a running search to stop cooperatively (1 = moveNow, 2 = cancel).
  export function signal(code) {
      if (!signalView) {
          return false;
      }
      Atomics.store(signalView, 0, code);
      return true;
  }
  ```

### 2.4 Update `Stello.Net/Stello.Web/Worker/EngineWorker.cs`
1. Declare `[JSImport("pollSignal", "engine-worker")] private static partial int PollSignal();`.
2. In `ChooseMove(string request)`:
   ```csharp
   [JSExport]
   public static string ChooseMove(string request)
   {
       (Board board, Player player, SearchLimits limits) = EngineProtocol.ReadRequest(request);
       using var cancelCts = new CancellationTokenSource();
       using var moveNowCts = new CancellationTokenSource();
       var poster = new ProgressPoster(cancelCts, moveNowCts);
       SearchResult result = Computer.ChooseMove(board, player, limits, poster, cancelCts.Token, moveNowCts.Token);
       return EngineProtocol.WriteResult(result);
   }
   ```
3. Inside `ProgressPoster.Report(SearchInfo info)`:
   ```csharp
   int signal = PollSignal();
   if (signal == 2)
   {
       cancelCts.Cancel();
       return;
   }
   if (signal == 1)
   {
       moveNowCts.Cancel();
       return;
   }
   ```

### 2.5 Update `Stello.Net/Stello.Web/Services/WebEngineHost.cs`
In `ChooseMoveAsync` (lines 62–83):
When `Task.WhenAny(search, stopped.Task)` completes via `stopped.Task`:
1. Determine `bool isMoveNow = await stopped.Task;`.
2. Try signaling cooperatively first:
   ```csharp
   bool signaled = client.Invoke<bool>("signal", isMoveNow ? 1 : 2);
   if (signaled)
   {
       Task completed = await Task.WhenAny(search, Task.Delay(300));
       if (completed == search)
       {
           if (!isMoveNow)
           {
               Forget(search);
               throw new OperationCanceledException(cancellationToken);
           }
           try
           {
               return EngineProtocol.ReadResult(await search);
           }
           catch (JSException)
           {
               // Fallback if worker threw
           }
       }
   }
   ```
3. Only if `!signaled` or the 300ms wait timed out, fall back to the existing `Forget(search); Restart(client);` and `EngineProtocol.MoveNowResult(...)`!

---

## Phase 3: Decouple `<AnalysisPanel>` Rendering (`Stello.Net/Stello.Web`)

### 3.1 Create `Stello.Net/Stello.Web/Components/AnalysisPanel.razor`
Create `AnalysisPanel.razor` so that 10 Hz `AnalysisViewModel` updates only re-render the `<aside class="panel analysis">` element and never trigger `Home.razor` or `Board.razor`'s 64-square `OnParametersSet()` loop:
```razor
@using System.ComponentModel
@implements IDisposable

<aside class="panel analysis" aria-label="Analysis">
    <h2>Analysis</h2>
    <dl>
        <dt>Move:</dt><dd>@Analysis.Move</dd>
        <dt>Depth:</dt><dd>@Analysis.Depth</dd>
        <dt>Value:</dt><dd>@Analysis.Value</dd>
        <dt>Best move:</dt><dd>@Analysis.BestMove</dd>
        <dt>Nodes:</dt><dd>@Analysis.Nodes</dd>
        <dt>Evaluations:</dt><dd>@Analysis.Evaluations</dd>
        <dt>Time:</dt><dd>@Analysis.Time</dd>
    </dl>
</aside>

@code {
    private bool _renderPending;

    [Parameter, EditorRequired]
    public AnalysisViewModel Analysis { get; set; } = default!;

    protected override void OnInitialized() => Analysis.PropertyChanged += OnChanged;

    public void Dispose() => Analysis.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_renderPending)
        {
            return;
        }

        _renderPending = true;
        _ = InvokeAsync(async () =>
        {
            await Task.Yield();
            _renderPending = false;
            StateHasChanged();
        });
    }
}
```

### 3.2 Update `Stello.Net/Stello.Web/Pages/Home.razor`
1. Replace the inline `<aside class="panel analysis">...</aside>` block (lines 65–76) with:
   ```razor
   <AnalysisPanel Analysis="Vm.Analysis" />
   ```
2. **Remove** `Vm.Analysis.PropertyChanged += OnChanged;` (line 134) and `Vm.Analysis.PropertyChanged -= OnChanged;` (line 160) from `Home.razor`.

---

## Phase 4: Responsive Design, Mobile Landscape Fix, & Touch Action Controls (`Stello.Net/Stello.Web`)

### 4.1 Add Touch Action Buttons & Thinking Progress Bar (`Stello.Net/Stello.Web/Pages/Home.razor`)
Inside `<section class="panel status" role="status" aria-label="Status">` in `Home.razor` (lines 52–61):
1. Add a visual indeterminate thinking bar right below `<p>@Vm.Status</p>` when `Vm.IsThinking` is true:
   ```razor
   @if (Vm.IsThinking)
   {
       <div class="thinking-bar" role="progressbar" aria-label="Computer thinking">
           <div class="thinking-progress"></div>
       </div>
   }
   ```
2. At the bottom of `<section class="panel status">`, add quick-action buttons so mobile, tablet, and desktop users can trigger primary actions with a single tap without opening dropdown menus:
   ```razor
   <div class="action-buttons">
       <button class="btn" disabled="@(!Vm.NewGameCommand.CanExecute(null))" @onclick="() => RunAsync(Vm.NewGameCommand)">New</button>
       <button class="btn" disabled="@(!Vm.UndoCommand.CanExecute(null))" @onclick="() => RunAsync(Vm.UndoCommand)">Back</button>
       <button class="btn" disabled="@(!Vm.RedoCommand.CanExecute(null))" @onclick="() => RunAsync(Vm.RedoCommand)">Forward</button>
       @if (Vm.IsThinking)
       {
           <button class="btn btn-primary action-span" @onclick="() => RunAsync(Vm.MoveNowCommand)">Move Now</button>
       }
   </div>
   ```

### 4.2 Upgrade Responsive CSS (`Stello.Net/Stello.Web/wwwroot/css/app.css`)
1. **Dynamic Viewport Height (`100dvh`)**:
   In `.app` (`app.css:15-19`), add `height: 100dvh;` after `height: 100vh;` so mobile browsers with collapsible address bars do not clip the bottom of `.content`:
   ```css
   .app {
       display: flex;
       flex-direction: column;
       height: 100vh;
       height: 100dvh;
   }
   ```
2. **Thinking Bar & Quick Action Buttons Styling**:
   Add styles for `.thinking-bar`, `.thinking-progress`, and `.action-buttons` inside `app.css` (and disable `.thinking-progress` animation inside the existing `@media (prefers-reduced-motion: reduce)` block at line 306):
   ```css
   .thinking-bar {
       width: 100%;
       height: 4px;
       background: #e2e8f0;
       border-radius: 2px;
       overflow: hidden;
       margin: 0.35rem 0;
   }

   .thinking-progress {
       width: 45%;
       height: 100%;
       background: #1e7a3c;
       border-radius: 2px;
       animation: thinking-slide 1.2s infinite ease-in-out;
   }

   @keyframes thinking-slide {
       0% { transform: translateX(-100%); }
       100% { transform: translateX(250%); }
   }

   .action-buttons {
       display: grid;
       grid-template-columns: repeat(3, 1fr);
       gap: 0.35rem;
       margin-top: 0.6rem;
   }

   .action-buttons .btn {
       padding: 0.35rem 0.4rem;
       border: 1px solid #bbb;
       border-radius: 4px;
       background: #f8f8f8;
       font-size: 0.85rem;
       font-weight: 600;
       cursor: pointer;
   }

   .action-buttons .btn:hover:not(:disabled) {
       background: #e5f1fb;
       border-color: #0078d4;
   }

   .action-buttons .btn:disabled {
       opacity: 0.5;
       cursor: default;
   }

   .action-buttons .btn-primary {
       background: #1e7a3c;
       color: #fff;
       border-color: #145a2b;
   }

   .action-buttons .btn-primary:hover:not(:disabled) {
       background: #23893f;
   }

   .action-buttons .action-span {
       grid-column: 1 / -1;
   }
   ```
3. **Fix Mobile Landscape & Portrait Breakpoints (`app.css:379-395`)**:
   Replace the unconditional `@media (max-width: 700px)` block with orientation-aware rules:
   - **Compact Landscape (`max-width: 820px` and `min-aspect-ratio: 1/1`)**: Keep `flex-direction: row` with a narrower `200px` side panel (`.board-area { width: min(100cqh, 100cqw - 212px); }`) so landscape phones keep the board and side panels side-by-side without clipping.
   - **Portrait / Narrow Screens (`(max-width: 700px) and (max-aspect-ratio: 1/1), (max-width: 500px)`)**: Switch `.content` to `flex-direction: column` and cap `.board-area` at `width: min(100cqw, 62cqh)` so the Status panel and action buttons remain visible above the fold without requiring vertical scrolling:
   ```css
   @media (max-width: 820px) and (min-aspect-ratio: 1/1) {
       .board-area {
           width: min(100cqh, 100cqw - 212px);
       }

       .side-panels {
           width: 200px;
       }
   }

   @media (max-width: 700px) and (max-aspect-ratio: 1/1), (max-width: 500px) {
       .content {
           flex-direction: column;
           justify-content: flex-start;
           align-items: center;
       }

       .board-area {
           width: min(100cqw, 62cqh);
       }

       .side-panels, .side-panels.beside-wood {
           align-self: stretch;
           width: auto;
           margin-top: 0;
       }
   }
   ```

---

## Phase 5: Verification & Testing

1. Add a unit test in `Stello.Net/Stello.Engine.Tests/SearchEngineTests.cs` verifying that `SearchEngine.Search` reports periodic progress during search and cleanly returns the best move found so far when `moveNowToken` is cancelled.
2. Run the full solution build and test suite:
   ```powershell
   dotnet build Stello.Net/Stello.Net.slnx
   dotnet test Stello.Net/Stello.Net.slnx
   ```
3. Verify zero compiler warnings or errors across `Stello.Engine`, `Stello.App`, `Stello.Net`, `Stello.Web`, and both test projects.

