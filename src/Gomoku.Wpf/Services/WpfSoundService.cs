using System.IO;
using System.Media;
using Gomoku.App.Models;
using Gomoku.App.Services;

namespace Gomoku.Wpf.Services;

public sealed class WpfSoundService : ISoundService
{
    public bool IsMuted { get; set; }

    public Task PlaySoundAsync(SoundType sound)
    {
        if (IsMuted) return Task.CompletedTask;

        try
        {
            byte[] wavBytes = SoundWaves.Create(sound);
            using var stream = new MemoryStream(wavBytes);
            using var player = new SoundPlayer(stream);
            player.Play();
        }
        catch
        {
            // Silently ignore audio errors on machines without audio devices
        }

        return Task.CompletedTask;
    }
}

