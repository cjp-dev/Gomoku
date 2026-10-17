using Gomoku.App.Models;

namespace Gomoku.App.Services;

/// <summary>
/// Audio feedback service for stone placements, victories, fouls, and alerts.
/// </summary>
public interface ISoundService
{
    Task PlaySoundAsync(SoundType sound);

    bool IsMuted { get; set; }
}

