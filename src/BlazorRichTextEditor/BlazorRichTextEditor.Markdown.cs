using Markdig;
using Microsoft.AspNetCore.Components;

namespace BlazorRichTextEditor;

// Phase 4: Markdown output mode. When Format is Markdown the bound Value is Markdown:
// incoming Markdown is rendered to HTML for the surface, and surface HTML is converted back
// to Markdown for ValueChanged. HTML mode (default) is unchanged.
public partial class BlazorRichTextEditor
{
    /// <summary>Content format exposed through <c>Value</c>/<c>ValueChanged</c>. HTML by default.</summary>
    [Parameter] public RichTextFormat Format { get; set; } = RichTextFormat.Html;

    private static readonly Markdig.MarkdownPipeline MarkdownPipeline =
        new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static readonly ReverseMarkdown.Converter HtmlToMarkdown =
        new(new ReverseMarkdown.Config { UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass, GithubFlavored = true });

    /// <summary>Converts surface HTML to the bound representation (HTML or Markdown).</summary>
    private string ToBoundValue(string html)
        => Format == RichTextFormat.Markdown ? HtmlToMarkdown.Convert(html) : html;

    /// <summary>Converts an incoming bound value (HTML or Markdown) to surface HTML.</summary>
    private string ToSurfaceHtml(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return Format == RichTextFormat.Markdown
            ? Markdig.Markdown.ToHtml(value, MarkdownPipeline)
            : value;
    }
}
