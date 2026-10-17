using Gomoku.App.Models;

namespace Gomoku.App.Services;

/// <summary>
/// Synthesizes procedural game sounds as 16-bit mono PCM WAV bytes without requiring external audio files.
/// </summary>
public static class SoundWaves
{
    public const int SampleRate = 22_050;
    private static readonly Dictionary<SoundType, byte[]> Cache = [];

    public static byte[] Create(SoundType sound)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(sound, out byte[]? wav))
            {
                wav = ToWav(sound switch
                {
                    SoundType.StonePlaced => StoneSnap(0.08),
                    SoundType.Win => Notes([523.25, 659.25, 783.99, 1046.50, 1318.5], 0.12, 0.75),
                    SoundType.Loss => Notes([392.00, 311.13, 261.63], 0.18, 0.6),
                    SoundType.Draw => Notes([440.0, 440.0], 0.15, 0.5),
                    SoundType.IllegalMove => Sweep(220, 110, 0.15, 0.6),
                    SoundType.Undo => Sweep(300, 600, 0.08, 0.5),
                    SoundType.GameStart => Notes([440.0, 659.25], 0.12, 0.6),
                    _ => StoneSnap(0.08)
                });
                Cache[sound] = wav;
            }

            return wav;
        }
    }

    private static short[] StoneSnap(double seconds)
    {
        int count = (int)(SampleRate * seconds);
        var samples = new short[count];
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            // Stone clack: high pitch impact dropping sharply
            double freq = 700 - 400 * t;
            double phase = 2 * Math.PI * freq * i / SampleRate;
            double envelope = Math.Exp(-18 * t);
            samples[i] = (short)(short.MaxValue * 0.85 * envelope * Math.Sin(phase));
        }
        return samples;
    }

    private static short[] Sweep(double fromHz, double toHz, double seconds, double volume)
    {
        int count = (int)(SampleRate * seconds);
        var samples = new short[count];
        double phase = 0;
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            phase += 2 * Math.PI * (fromHz + (toHz - fromHz) * t) / SampleRate;
            samples[i] = (short)(short.MaxValue * volume * Math.Exp(-5 * t) * Math.Sin(phase));
        }
        return samples;
    }

    private static short[] Notes(double[] freqs, double noteSeconds, double volume)
    {
        int perNote = (int)(SampleRate * noteSeconds);
        var samples = new short[perNote * freqs.Length];
        for (int n = 0; n < freqs.Length; n++)
        {
            double freq = freqs[n];
            for (int i = 0; i < perNote; i++)
            {
                double t = (double)i / perNote;
                double phase = 2 * Math.PI * freq * i / SampleRate;
                double envelope = Math.Exp(-3.5 * t);
                samples[n * perNote + i] = (short)(short.MaxValue * volume * envelope * Math.Sin(phase));
            }
        }
        return samples;
    }

    private static byte[] ToWav(short[] pcm)
    {
        int subchunk2Size = pcm.Length * 2;
        int chunkSize = 36 + subchunk2Size;
        var bytes = new byte[44 + subchunk2Size];
        using var stream = new MemoryStream(bytes);
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(chunkSize);
        writer.Write("WAVE"u8);

        // fmt chunk
        writer.Write("fmt "u8);
        writer.Write(16);             // subchunk1 size (16 for PCM)
        writer.Write((short)1);        // audio format (1 = PCM)
        writer.Write((short)1);        // num channels (1 = mono)
        writer.Write(SampleRate);      // sample rate
        writer.Write(SampleRate * 2);  // byte rate
        writer.Write((short)2);        // block align
        writer.Write((short)16);       // bits per sample

        // data chunk
        writer.Write("data"u8);
        writer.Write(subchunk2Size);
        for (int i = 0; i < pcm.Length; i++)
        {
            writer.Write(pcm[i]);
        }

        return bytes;
    }
}

