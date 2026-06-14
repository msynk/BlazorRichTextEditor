using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 4: find and replace.
public partial class BlazorRichTextEditor
{
    private bool _showFind;
    private string _findTerm = "";
    private string _replaceTerm = "";
    private bool _findCaseSensitive;
    private string _findCount = "";

    private void ToggleFind()
    {
        _showFind = !_showFind;
        if (!_showFind)
        {
            _findTerm = "";
            _replaceTerm = "";
            _findCount = "";
            _ = ClearFindAsync();
        }
        ClearInlineError();
    }

    private async Task ClearFindAsync()
    {
        if (_module is not null) await _module.InvokeVoidAsync("clearFind", _editor);
    }

    private async Task RunFindAsync()
    {
        if (_module is null) return;
        if (string.IsNullOrEmpty(_findTerm))
        {
            _findCount = "";
            await _module.InvokeVoidAsync("clearFind", _editor);
            return;
        }
        if (_findTerm.Length > 1000)
        {
            await RaiseErrorAsync(new RichTextError("invalid-find", "Search term is too long."));
            return;
        }
        var count = await _module.InvokeAsync<int>("find", _editor, _findTerm, _findCaseSensitive);
        _findCount = count == 0 ? "No matches" : $"{count} match{(count == 1 ? "" : "es")}";
    }

    private async Task ReplaceCurrentAsync()
    {
        if (ReadOnly || _module is null || string.IsNullOrEmpty(_findTerm)) return;
        await _module.InvokeVoidAsync("replaceCurrent", _editor, _findTerm, _replaceTerm, _findCaseSensitive);
        await RunFindAsync();
    }

    private async Task ReplaceAllAsync()
    {
        if (ReadOnly || _module is null || string.IsNullOrEmpty(_findTerm)) return;
        var n = await _module.InvokeAsync<int>("replaceAll", _editor, _findTerm, _replaceTerm, _findCaseSensitive);
        _findCount = $"{n} replaced";
    }
}
