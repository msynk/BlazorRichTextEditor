using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 1: image insertion (URL, drag-drop, paste, upload callback), color, and font.
public partial class BlazorRichTextEditor
{
    /// <summary>
    /// Invoked to persist an image binary, returning the URL to embed. When null, dropped or
    /// pasted images are embedded as inline data URLs.
    /// </summary>
    [Parameter] public Func<RichTextImageUpload, Task<string?>>? OnImageUpload { get; set; }

    /// <summary>Font families offered in the font-family selector. Null/empty uses defaults.</summary>
    [Parameter] public IReadOnlyList<string>? FontFamilies { get; set; }

    /// <summary>Font sizes offered in the font-size selector. Null/empty uses defaults.</summary>
    [Parameter] public IReadOnlyList<string>? FontSizes { get; set; }

    private static readonly string[] DefaultFontFamilies =
        { "Arial", "Georgia", "Tahoma", "Times New Roman", "Verdana", "Courier New" };

    private static readonly string[] DefaultFontSizes =
        { "10px", "12px", "14px", "16px", "18px", "24px", "32px" };

    private IReadOnlyList<string> EffectiveFontFamilies
        => FontFamilies is { Count: > 0 } ? FontFamilies : DefaultFontFamilies;

    private IReadOnlyList<string> EffectiveFontSizes
        => FontSizes is { Count: > 0 } ? FontSizes : DefaultFontSizes;

    // ---- image insertion ----
    private bool _showImageInput;
    private string _imageUrl = "";

    private void ToggleImageInput()
    {
        _showImageInput = !_showImageInput;
        _imageUrl = "";
        ClearInlineError();
    }

    private async Task ApplyImageUrlAsync()
    {
        var url = _imageUrl.Trim();
        if (!IsAcceptableImageUrl(url))
        {
            await RaiseErrorAsync(new RichTextError("invalid-url", "That image URL is not valid."));
            return;
        }
        if (_module is not null)
            await _module.InvokeVoidAsync("insertImageUrl", _editor, url);
        _showImageInput = false;
        _imageUrl = "";
    }

    private static bool IsAcceptableImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048) return false;
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Called by the bridge for each dropped/pasted image; returns the URL to embed.</summary>
    [JSInvokable]
    public async Task<string?> ResolveImageUrl(string fileName, string contentType, string base64)
    {
        if (OnImageUpload is null)
            return $"data:{contentType};base64,{base64}";   // inline data URL fallback (Req 7.7)

        try
        {
            var bytes = Convert.FromBase64String(base64);
            var url = await OnImageUpload(new RichTextImageUpload(fileName, contentType, bytes));
            if (string.IsNullOrWhiteSpace(url))
            {
                await RaiseErrorAsync(new RichTextError("upload-failed", $"Upload of \"{fileName}\" did not return a URL."));
                return null;
            }
            return url;
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync(new RichTextError("upload-failed", $"Upload of \"{fileName}\" failed: {ex.Message}"));
            return null;
        }
    }

    /// <summary>Called by the bridge to surface client-side validation errors (e.g. bad file).</summary>
    [JSInvokable]
    public async Task OnClientError(string code, string message)
        => await RaiseErrorAsync(new RichTextError(code, message));

    // ---- color ----
    private async Task ApplyColorAsync(string kind, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (ReadOnly || _module is null || string.IsNullOrWhiteSpace(value)) return;
        await _module.InvokeVoidAsync("applyColor", _editor, kind, value);
    }

    // ---- font ----
    private async Task ApplyFontAsync(string kind, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (ReadOnly || _module is null || string.IsNullOrWhiteSpace(value)) return;
        await _module.InvokeVoidAsync("applyFont", _editor, kind, value);
    }

    // ---- indent / script ----
    private Task IndentAsync() => ExecAsync("indent");
    private Task OutdentAsync() => ExecAsync("outdent");
    private Task SubscriptAsync() => ExecAsync("subscript");
    private Task SuperscriptAsync() => ExecAsync("superscript");
}
