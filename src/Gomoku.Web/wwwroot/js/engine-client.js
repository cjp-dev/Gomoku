// Page-side controller for the Gomoku engine Web Worker.
const workerUrl = new URL('./engine-worker.js', import.meta.url);
const fallbackDotnetUrl = import.meta.resolve('../_framework/dotnet.js');
let threadedDotnetUrl = null;
try {
    threadedDotnetUrl = import.meta.resolve('../engine/_framework/dotnet.js');
} catch {}

let worker = null;
let nextId = 1;
const pending = new Map();
const canShareMemory = typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated;
const signalBuffer = canShareMemory ? new SharedArrayBuffer(4) : null;
const signalView = signalBuffer ? new Int32Array(signalBuffer) : null;
let isThreadedWorker = false;

function rejectPending(message) {
    for (const { reject } of pending.values()) {
        reject(new Error(message));
    }
    pending.clear();
}

function post(command, args, includeBuffer = false) {
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

function spawnWorker(progressTarget, dotnetUrl, threaded) {
    terminate();
    const url = new URL(workerUrl);
    url.searchParams.set('dotnet', dotnetUrl);
    url.searchParams.set('threaded', threaded ? 'true' : 'false');
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
    isThreadedWorker = threaded;
    return post('init', [], true);
}

export async function start(progressTarget) {
    const canUseThreads = typeof SharedArrayBuffer !== 'undefined' && self.crossOriginIsolated;

    if (canUseThreads && threadedDotnetUrl) {
        try {
            console.log('[Gomoku.Engine] Attempting to start multithreaded WASM engine...');
            await timeoutPromise(spawnWorker(progressTarget, threadedDotnetUrl, true), 12000, 'Threaded engine init timeout');
            console.log('[Gomoku.Engine] Multithreaded WASM engine started successfully.');
            return;
        } catch (err) {
            console.warn('[Gomoku.Engine] Multithreaded engine failed to start; falling back to single-threaded:', err);
            terminate();
        }
    }

    console.log('[Gomoku.Engine] Starting single-threaded WASM engine...');
    await spawnWorker(progressTarget, fallbackDotnetUrl, false);
    console.log('[Gomoku.Engine] Single-threaded WASM engine started successfully.');
}

export function call(command, ...args) {
    if (!worker) {
        return Promise.reject(new Error('The engine is not running.'));
    }
    return post(command, args);
}

// Signals cooperative stop (1 = moveNow, 2 = cancel). Returns true if signaling is supported.
export function signal(code) {
    let handled = false;
    if (signalView) {
        Atomics.store(signalView, 0, code);
        handled = true;
    }
    if (worker) {
        worker.postMessage({ command: 'signal', args: [code] });
        handled = true;
    }
    return handled;
}

export function terminate() {
    worker?.terminate();
    worker = null;
    isThreadedWorker = false;
    rejectPending('The engine was stopped.');
}

export function isThreaded() {
    return isThreadedWorker;
}
