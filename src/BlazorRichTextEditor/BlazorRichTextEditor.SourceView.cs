using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 1: HTML source view. While active, the WYSIWYG surface is replaced by a raw-HTML
// textarea and the formatting controls are disabled. On exit the edited HTML is sanitized,
// validated, rendered, and emitted via ValueChanged.
public partial class BlazorRichTextEditor
{
    private bool _inSourceView;
    private string _sourceText = "";

    private async Task ToggleSourceViewAsync()
    {
        if (ReadOnly) return;
        ClearInlineError();

        if (!_inSourceView)
        {
            _sourceText = await GetHtmlAsync();
            _inSourceView = true;
            StateHasChanged();
            return;
        }

        // Exiting: validate, sanitize, render.
        if (!LooksLikeValidHtml(_sourceText))
        {
            await RaiseErrorAsync(new RichTextError("invalid-html", "The HTML could not be parsed; fix it before leaving source view."));
            return;
        }

        var sanitized = _sourceText;
        if (_module is not null)
            sanitized = await _module.InvokeAsync<string>("sanitizeHtml", _editor, _sourceText);

        _inSourceView = false;
        _currentHtml = sanitized;
        if (_module is not null)
            await _module.InvokeVoidAsync("setHtml", _editor, sanitized);
        StateHasChanged();
        var bound = ToBoundValue(sanitized);
        _currentValue = bound;
        await ValueChanged.InvokeAsync(bound);
    }

    // Lightweight well-formedness check (Req 12.5): reject mismatched angle brackets.
    private static bool LooksLikeValidHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return true;
        var open = html.Count(c => c == '<');
        var close = html.Count(c => c == '>');
        return open == close;
    }

    private void OnSourceTextChanged(Microsoft.AspNetCore.Components.ChangeEventArgs e)
        => _sourceText = e.Value?.ToString() ?? "";
}
