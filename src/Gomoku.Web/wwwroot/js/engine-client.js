// Page-side controller for the Gomoku AI engine.
//
// Architecture in .NET 10 WebAssembly:
// - When crossOriginIsolated + SharedArrayBuffer are available, the multithreaded
//   Gomoku.Web.Engine runtime is booted directly in the Window context. Its WasmEnableThreads=true
//   Mono runtime automatically runs 100% of managed C# code off the UI thread on a background
//   Deputy Thread + Web Worker pthread pool (up to 8 Lazy SMP search threads).
// - When SharedArrayBuffer is unavailable (or if the threaded runtime fails to load),
//   falls back to running the single-threaded runtime inside a Dedicated Web Worker (engine-worker.js).

const workerUrl = new URL('./engine-worker.js', import.meta.url);
const fallbackDotnetUrl = import.meta.resolve('../_framework/dotnet.js');
let threadedDotnetUrl = null;
try {
    threadedDotnetUrl = import.meta.resolve('../engine/_framework/dotnet.js');
} catch {}

// 16 pre-warmed Emscripten pthreads comfortably cover up to 8 Lazy SMP search threads
// plus Mono's internal Deputy, IO, Timer, Finalizer, and ThreadPool Gate threads.
const PTHREAD_POOL_INITIAL_SIZE = 16;

let threadedEngine = null;
let threadedInitPromise = null;
let activeProgressTarget = null;

let worker = null;
let nextId = 1;
const pending = new Map();
const canShareMemory = typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated;
const signalBuffer = canShareMemory ? new SharedArrayBuffer(4) : null;
const signalView = signalBuffer ? new Int32Array(signalBuffer) : null;
let isThreadedMode = false;

function rejectPending(message) {
    for (const { reject } of pending.values()) {
        reject(new Error(message));
    }
    pending.clear();
}

function postToWorker(command, args, includeBuffer = false) {
    if (signalView) {
        Atomics.store(signalView, 0, 0);
    }
    const id = nextId++;
    const response = new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
    worker.postMessage(includeBuffer && signalBuffer ? { id, command, args, signalBuffer } : { id, command, args });
    return response;
}

function timeoutPromise(promise, ms, message) {
    return new Promise((resolve, reject) => {
        const id = setTimeout(() => reject(new Error(message)), ms);
        promise.then(
            res => { clearTimeout(id); resolve(res); },
            err => { clearTimeout(id); reject(err); }
        );
    });
}

async function initThreadedEngineOnWindow() {
    if (threadedEngine) {
        return threadedEngine;
    }
    if (!threadedInitPromise) {
        threadedInitPromise = (async () => {
            const { dotnet } = await import(threadedDotnetUrl);
            const runtime = await dotnet
                .withConfig({ pthreadPoolInitialSize: PTHREAD_POOL_INITIAL_SIZE })
                .create();
            const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
            const engine = exports.Gomoku.Web.Engine.EngineWorker;
            await engine.InitAsync();
            threadedEngine = engine;
            return engine;
        })();
    }
    return threadedInitPromise;
}

async function forwardDrainedProgress(engine, target) {
    if (!engine || !target) return;
    const batch = await engine.DrainProgressAsync();
    if (!batch) return;
    const lines = batch.split('\n');
    for (const line of lines) {
        if (line) {
            target.invokeMethodAsync('OnProgress', line);
        }
    }
}

function spawnFallbackWorker(progressTarget, dotnetUrl) {
    terminateFallbackWorker();
    const url = new URL(workerUrl);
    url.searchParams.set('dotnet', dotnetUrl);
    const current = new Worker(url, { type: 'module' });

    current.addEventListener('message', e => {
        if (current !== worker) return;
        const message = e.data;
        if (message.type === 'progress') {
            progressTarget.invokeMethodAsync('OnProgress', message.json);
            return;
        }
        const request = pending.get(message.id);
        if (request) {
            pending.delete(message.id);
            if (message.error !== undefined) {
                request.reject(new Error(message.error));
            } else {
                request.resolve(message.result);
            }
        }
    });

    current.addEventListener('error', e => {
        if (current === worker) {
            rejectPending(`The engine could not run: ${e.message ?? 'unknown error'}`);
        }
    });

    worker = current;
    isThreadedMode = false;
    return postToWorker('init', [], true);
}

function terminateFallbackWorker() {
    worker?.terminate();
    worker = null;
    rejectPending('The engine was stopped.');
}

export async function start(progressTarget) {
    activeProgressTarget = progressTarget;
    const canUseThreads = typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated;

    if (canUseThreads && threadedDotnetUrl) {
        try {
            console.log('[Gomoku.Engine] Attempting to start multithreaded WASM engine...');
            await timeoutPromise(initThreadedEngineOnWindow(), 20000, 'Threaded engine init timeout');
            isThreadedMode = true;
            console.log('[Gomoku.Engine] Multithreaded WASM engine started successfully.');
            return;
        } catch (err) {
            threadedInitPromise = null;
            threadedEngine = null;
            isThreadedMode = false;
            console.warn('[Gomoku.Engine] Multithreaded engine failed to start; falling back to single-threaded worker:', err);
        }
    }

    console.log('[Gomoku.Engine] Starting single-threaded WASM engine worker...');
    await spawnFallbackWorker(progressTarget, fallbackDotnetUrl);
    console.log('[Gomoku.Engine] Single-threaded WASM engine started successfully.');
}

export async function call(command, ...args) {
    if (isThreadedMode && threadedEngine) {
        const engine = threadedEngine;
        const target = activeProgressTarget;

        switch (command) {
            case 'init':
                await engine.InitAsync();
                return null;

            case 'chooseMove': {
                let draining = false;
                const timer = setInterval(async () => {
                    if (draining) return;
                    draining = true;
                    try {
                        await forwardDrainedProgress(engine, target);
                    } catch {
                        // Ignore transient drain errors
                    } finally {
                        draining = false;
                    }
                }, 50);

                try {
                    const result = await engine.ChooseMoveAsync(...args);
                    await forwardDrainedProgress(engine, target);
                    return result;
                } finally {
                    clearInterval(timer);
                }
            }

            case 'clearTt':
                await engine.ClearTranspositionTableAsync();
                return null;

            default:
                throw new Error(`Unknown engine command: ${command}`);
        }
    }

    if (!worker) {
        throw new Error('The engine is not running.');
    }
    return postToWorker(command, args);
}

// Signals cooperative stop (1 = moveNow, 2 = cancel). Returns true if signaling is supported.
export async function signal(code) {
    if (isThreadedMode && threadedEngine) {
        try {
            await threadedEngine.SignalAsync(code);
            return true;
        } catch {
            return false;
        }
    }

    if (signalView) {
        Atomics.store(signalView, 0, code);
        return true;
    }
    return false;
}

export function terminate() {
    if (isThreadedMode && threadedEngine) {
        // Keep the singleton Window-hosted Mono runtime and its Transposition Table alive;
        // cooperatively cancel any in-flight search immediately.
        threadedEngine.SignalAsync(2).catch(() => {});
        return;
    }
    terminateFallbackWorker();
}

export function isThreaded() {
    return isThreadedMode;
}
