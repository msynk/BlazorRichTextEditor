using System.Text.RegularExpressions;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 2: media embeds and horizontal rule.
public partial class BlazorRichTextEditor
{
    private bool _showMediaInput;
    private string _mediaUrl = "";

    private void ToggleMediaInput()
    {
        _showMediaInput = !_showMediaInput;
        _mediaUrl = "";
        ClearInlineError();
    }

    private async Task ApplyMediaAsync()
    {
        var url = _mediaUrl.Trim();
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await RaiseErrorAsync(new RichTextError("invalid-url", "That media URL is not valid."));
            return;
        }

        var html = BuildMediaEmbed(uri);
        if (html is null)
        {
            await RaiseErrorAsync(new RichTextError("media-not-allowed", "That media type or host is not supported."));
            return;
        }

        if (_module is not null)
            await _module.InvokeVoidAsync("insertMedia", _editor, html);
        _showMediaInput = false;
        _mediaUrl = "";
    }

    private static string? BuildMediaEmbed(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        var url = uri.AbsoluteUri;

        // YouTube
        var ytId = TryGetYouTubeId(uri);
        if (ytId is not null)
            return $"<iframe width=\"560\" height=\"315\" src=\"https://www.youtube-nocookie.com/embed/{ytId}\" " +
                   "frameborder=\"0\" allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe>";

        // Vimeo
        if (host.Contains("vimeo.com"))
        {
            var m = Regex.Match(uri.AbsolutePath, @"/(\d+)");
            if (m.Success)
                return $"<iframe src=\"https://player.vimeo.com/video/{m.Groups[1].Value}\" width=\"560\" height=\"315\" frameborder=\"0\" allow=\"autoplay; fullscreen; picture-in-picture\" allowfullscreen></iframe>";
        }

        // Direct media files
        var path = uri.AbsolutePath.ToLowerInvariant();
        if (path.EndsWith(".mp4") || path.EndsWith(".webm") || path.EndsWith(".ogv"))
            return $"<video src=\"{Esc(url)}\" controls width=\"560\"></video>";
        if (path.EndsWith(".mp3") || path.EndsWith(".ogg") || path.EndsWith(".wav"))
            return $"<audio src=\"{Esc(url)}\" controls></audio>";

        return null;
    }

    private static string? TryGetYouTubeId(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("youtu.be"))
            return uri.AbsolutePath.Trim('/').Split('/')[0] is { Length: > 0 } id ? id : null;
        if (host.Contains("youtube.com"))
        {
            var v = GetQueryValue(uri.Query, "v");
            if (!string.IsNullOrEmpty(v)) return v;
            var m = Regex.Match(uri.AbsolutePath, @"/embed/([\w-]+)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            if (pair.AsSpan(0, eq).Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return null;
    }

    private static string Esc(string s)
        => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---- horizontal rule ----
    private async Task InsertRuleAsync()
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("exec", _editor, "insertHorizontalRule", null);
    }
}
