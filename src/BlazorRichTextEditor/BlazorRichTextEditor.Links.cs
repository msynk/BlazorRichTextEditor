using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Link insertion / editing. Phase 0 preserves the original create+unlink behavior;
// Phase 2 adds edit-existing-link prefill, validation, and remove affordances.
public partial class BlazorRichTextEditor
{
    private bool _showLinkInput;
    private string _linkUrl = "";

    private void ToggleLinkInput()
    {
        _showLinkInput = !_showLinkInput;
        if (_showLinkInput)
        {
            // Prefill when the selection is inside an existing link (Req 16.1).
            _linkUrl = _state.InLink && _state.LinkHref is not null ? _state.LinkHref : "";
        }
        else
        {
            _linkUrl = "";
        }
        ClearInlineError();
    }

    private async Task ApplyLinkAsync()
    {
        var url = _linkUrl.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            await RaiseErrorAsync(new RichTextError("invalid-url", "Enter a URL for the link."));
            return;
        }
        if (url.Length > 2048 || !IsAcceptableLinkUrl(url))
        {
            await RaiseErrorAsync(new RichTextError("invalid-url", "That link URL is not valid."));
            return;
        }
        if (_module is not null)
        {
            if (_state.InLink)
                await _module.InvokeVoidAsync("updateLink", _editor, url);   // Req 16.3
            else
                await _module.InvokeVoidAsync("createLink", _editor, url);
        }
        _showLinkInput = false;
        _linkUrl = "";
    }

    private async Task RemoveLinkAsync()
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("exec", _editor, "unlink", null);
        _showLinkInput = false;
        _linkUrl = "";
    }

    private async Task OnLinkKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ApplyLinkAsync();
        else if (e.Key == "Escape") ToggleLinkInput();
    }

    private static bool IsAcceptableLinkUrl(string url)
    {
        // Allow absolute http(s)/mailto/tel and site-relative URLs; reject script vectors.
        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("/") || url.StartsWith("#") || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
    }
}
