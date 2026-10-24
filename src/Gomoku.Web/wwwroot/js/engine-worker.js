// Web Worker that runs the Gomoku AI engine in its own .NET runtime.
const url = new URL(self.location.href);
const dotnetUrl = url.searchParams.get('dotnet');
const isThreaded = url.searchParams.get('threaded') === 'true';
let signalView = null;

const started = (async () => {
    const { dotnet } = await import(dotnetUrl);
    const runtime = await dotnet.create();
    if (!isThreaded) {
        runtime.setModuleImports('engine-worker', {
            postProgress: json => self.postMessage({ type: 'progress', json }),
            pollSignal: () => signalView ? Atomics.load(signalView, 0) : 0,
        });
    }
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    return isThreaded ? exports.Gomoku.Web.Engine.EngineWorker : exports.Gomoku.Web.Worker.EngineWorker;
})();

self.addEventListener('message', async e => {
    const { id, command, args, signalBuffer } = e.data;
    if (signalBuffer) {
        signalView = new Int32Array(signalBuffer);
    }
    if (command === 'signal') {
        try {
            const engine = await started;
            if (isThreaded && engine && engine.Signal) {
                engine.Signal(args[0]);
            }
        } catch {
            // Signal error ignored
        }
        return;
    }
    try {
        const engine = await started;
        let result;
        switch (command) {
            case 'init':
                result = engine.Init();
                break;
            case 'chooseMove':
                if (isThreaded) {
                    const timer = setInterval(() => {
                        let p;
                        while ((p = engine.DrainProgress()) !== null) {
                            self.postMessage({ type: 'progress', json: p });
                        }
                    }, 50);
                    try {
                        result = await engine.ChooseMoveAsync(...args);
                        let p;
                        while ((p = engine.DrainProgress()) !== null) {
                            self.postMessage({ type: 'progress', json: p });
                        }
                    } finally {
                        clearInterval(timer);
                    }
                } else {
                    result = engine.ChooseMove(...args);
                }
                break;
            case 'clearTt':
                result = engine.ClearTranspositionTable();
                break;
            default:
                throw new Error(`Unknown engine command: ${command}`);
        }
        self.postMessage({ type: 'response', id, result });
    } catch (error) {
        self.postMessage({ type: 'response', id, error: error?.message ?? String(error) });
    }
});
