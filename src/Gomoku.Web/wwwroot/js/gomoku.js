// Browser helpers for Gomoku .NET via JS interop
window.gomoku = (() => {
    const maxFileSize = 1_000_000;
    const sounds = new Map();
    let audioCtx = null;

    const shortcutKeys = 'noszyam';
    let keydown = null;

    function getAudioContext() {
        if (!audioCtx) {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (AudioContext) {
                audioCtx = new AudioContext();
            }
        }
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume().catch(() => {});
        }
        return audioCtx;
    }

    return {
        registerShortcuts(dotnet) {
            keydown = e => {
                if (e.key === 'F1') {
                    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
                        return;
                    }
                    e.preventDefault();
                    if (e.shiftKey) {
                        dotnet.invokeMethodAsync('OnShortcut', 'shift+f1');
                    } else if (!e.ctrlKey && !e.altKey && !e.metaKey) {
                        dotnet.invokeMethodAsync('OnShortcut', 'f1');
                    }
                    return;
                }
                if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey || e.key.length !== 1) {
                    return;
                }
                const key = e.key.toLowerCase();
                if (!shortcutKeys.includes(key) || e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) {
                    return;
                }
                e.preventDefault();
                dotnet.invokeMethodAsync('OnShortcut', key);
            };
            document.addEventListener('keydown', keydown);
        },

        unregisterShortcuts() {
            if (keydown) {
                document.removeEventListener('keydown', keydown);
                keydown = null;
            }
        },

        openTextFile(accept) {
            return new Promise((resolve, reject) => {
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = accept;
                input.addEventListener('cancel', () => resolve(null));
                input.addEventListener('change', async () => {
                    const file = input.files[0];
                    if (!file) {
                        resolve(null);
                    } else if (file.size > maxFileSize) {
                        reject(new Error('The file is too large.'));
                    } else {
                        resolve([file.name, await file.text()]);
                    }
                });
                input.click();
            });
        },

        downloadTextFile(name, text) {
            const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
            const link = document.createElement('a');
            link.href = url;
            link.download = name;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        },

        async registerSound(name, pcmBytes) {
            try {
                const ctx = getAudioContext();
                if (!ctx) return;
                let arrayBuffer;
                if (typeof pcmBytes === 'string') {
                    const binary = atob(pcmBytes);
                    const bytes = new Uint8Array(binary.length);
                    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
                    arrayBuffer = bytes.buffer;
                } else if (pcmBytes instanceof Uint8Array) {
                    arrayBuffer = pcmBytes.buffer.slice(pcmBytes.byteOffset, pcmBytes.byteOffset + pcmBytes.byteLength);
                } else if (pcmBytes && pcmBytes.buffer) {
                    arrayBuffer = pcmBytes.buffer;
                } else {
                    return;
                }
                const buffer = await ctx.decodeAudioData(arrayBuffer);
                sounds.set(name, buffer);
            } catch (err) {
                // Audio decode might fail if permissions suspended
            }
        },

        playSound(name) {
            try {
                const ctx = getAudioContext();
                if (!ctx) return;
                const buffer = sounds.get(name);
                if (!buffer) return;
                const source = ctx.createBufferSource();
                source.buffer = buffer;
                source.connect(ctx.destination);
                source.start(0);
            } catch (err) {
            }
        }
    };
})();

