// Gomoku's browser helpers (board focus, sounds); the shared ones are in GameShell's gameshell.js.
window.gomoku = (() => {
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
            audioCtx.resume().catch(() => {});
        }
        return audioCtx;
    }

    return {
        focusCell(x, y) {
            const el = document.querySelector(`.board-intersection[data-x="${x}"][data-y="${y}"]`);
            if (el && typeof el.focus === 'function') {
                el.focus();
            }
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
        },
    };
})();

