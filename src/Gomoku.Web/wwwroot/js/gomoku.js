// Browser helpers for Gomoku .NET via JS interop
window.gomoku = (() => {
    const maxFileSize = 1_000_000;
    const sounds = new Map();
    let audioCtx = null;

    function getAudioContext() {
        if (!audioCtx) {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (AudioContext) {
                audioCtx = new AudioContext();
            }
        }
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
        return audioCtx;
    }

    return {
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
                const buffer = await ctx.decodeAudioData(pcmBytes.buffer.slice(pcmBytes.byteOffset, pcmBytes.byteOffset + pcmBytes.byteLength));
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

