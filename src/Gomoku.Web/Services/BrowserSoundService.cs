using Microsoft.JSInterop;
using Gomoku.App.Models;
using Gomoku.App.Services;

namespace Gomoku.Web.Services;

public sealed class BrowserSoundService(IJSInProcessRuntime js) : ISoundService
{
    private bool _registered;

    public bool IsMuted { get; set; }

    public Task PlaySoundAsync(SoundType sound)
    {
        if (IsMuted) return Task.CompletedTask;

        try
        {
            if (!_registered)
            {
                foreach (SoundType each in Enum.GetValues<SoundType>())
                {
                    js.InvokeVoid("gomoku.registerSound", each.ToString(), SoundWaves.Create(each));
                }
                _registered = true;
            }

            js.InvokeVoid("gomoku.playSound", sound.ToString());
        }
        catch (JSException)
        {
            // Audio silently disabled if browser restricts autoplay
        }

        return Task.CompletedTask;
    }
}

