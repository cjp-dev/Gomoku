using Microsoft.JSInterop;
using Gomoku.App.Services;

namespace Gomoku.Web.Services;

public sealed class BrowserGameFileService(IJSRuntime js) : IGameFileService
{
    public async Task<string?> OpenGameFileAsync()
    {
        try
        {
            var res = await js.InvokeAsync<string[]?>("gomoku.openTextFile", ".sgf,.gomoku,.txt");
            if (res != null && res.Length == 2)
            {
                return res[1]; // text content
            }
        }
        catch (JSException)
        {
        }

        return null;
    }

    public async Task<bool> SaveGameFileAsync(string defaultFileName, string content, string filter)
    {
        try
        {
            await js.InvokeVoidAsync("gomoku.downloadTextFile", defaultFileName, content);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}

