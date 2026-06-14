namespace BlazorRichTextEditor;

/// <summary>
/// Toolbar button groups. Combine with bitwise OR to choose which groups appear, or use
/// <see cref="All"/> for the default (original) toolbar or <see cref="AllExtended"/> for
/// every available group. Existing bit positions are stable across versions.
/// </summary>
[Flags]
public enum BlazorRichTextEditorToolbar
{
    None = 0,
    History = 1 << 0,
    BlockFormat = 1 << 1,
    Inline = 1 << 2,
    Lists = 1 << 3,
    Blocks = 1 << 4,
    Link = 1 << 5,
    Alignment = 1 << 6,
    Clear = 1 << 7,

    // Extended groups (opt-in). Existing callers using All are unaffected.
    Image = 1 << 8,
    Color = 1 << 9,
    Font = 1 << 10,
    Indent = 1 << 11,
    Script = 1 << 12,
    Source = 1 << 13,
    Table = 1 << 14,
    Media = 1 << 15,
    Rule = 1 << 16,
    Emoji = 1 << 17,
    Find = 1 << 18,
    FullScreen = 1 << 19,
    Direction = 1 << 20,

    /// <summary>The original toolbar groups. Preserved for backwards compatibility.</summary>
    All = History | BlockFormat | Inline | Lists | Blocks | Link | Alignment | Clear,

    /// <summary>Every available toolbar group, including the extended ones.</summary>
    AllExtended = All | Image | Color | Font | Indent | Script | Source
                | Table | Media | Rule | Emoji | Find | FullScreen | Direction
}

/// <summary>
/// Snapshot of the current selection's formatting, reported by the JS bridge and used to
/// highlight active toolbar buttons. Property names map (case-insensitively) to the JS
/// state object; properties missing from the JS object default to inactive.
/// </summary>
public sealed class BlazorRichTextEditorSelectionState
{
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool StrikeThrough { get; set; }
    public bool OrderedList { get; set; }
    public bool UnorderedList { get; set; }
    public bool JustifyLeft { get; set; }
    public bool JustifyCenter { get; set; }
    public bool JustifyRight { get; set; }

    /// <summary>The current block tag (e.g. "p", "h1", "blockquote", "pre"), lowercase.</summary>
    public string Block { get; set; } = "";

    // Extended formatting state.
    public bool Subscript { get; set; }
    public bool Superscript { get; set; }

    /// <summary>Active foreground color of the selection, or null when mixed/none.</summary>
    public string? ForeColor { get; set; }

    /// <summary>Active background/highlight color of the selection, or null when mixed/none.</summary>
    public string? BackColor { get; set; }

    /// <summary>Active font family, or null when the selection spans multiple families.</summary>
    public string? FontName { get; set; }

    /// <summary>Active font size, or null when the selection spans multiple sizes.</summary>
    public string? FontSize { get; set; }

    /// <summary>Text direction of the selected block ("ltr"/"rtl"), or null.</summary>
    public string? Direction { get; set; }

    /// <summary>True when the selection sits inside a single hyperlink.</summary>
    public bool InLink { get; set; }

    /// <summary>The href of the link under the selection, or null when none/multiple.</summary>
    public string? LinkHref { get; set; }
}

/// <summary>
/// Structured facts about the editor content, computed by the JS bridge and used by C#
/// to classify emptiness and drive character/word counts.
/// </summary>
public readonly record struct RichTextContentFacts(
    bool HasText,
    bool HasEmbeddedContent,
    int CharacterCount,
    int WordCount)
{
    /// <summary>Content is empty when it has neither text nor embedded (non-text) content.</summary>
    public bool IsEmpty => !HasText && !HasEmbeddedContent;
}

/// <summary>An image to be persisted by the host's <c>OnImageUpload</c> delegate.</summary>
/// <param name="FileName">Original file name, when available.</param>
/// <param name="ContentType">MIME type, e.g. "image/png".</param>
/// <param name="Content">Raw image bytes.</param>
public sealed record RichTextImageUpload(string FileName, string ContentType, byte[] Content);

/// <summary>Output/round-trip content format.</summary>
public enum RichTextFormat
{
    /// <summary>HTML content (default; preserves original behavior).</summary>
    Html,
    /// <summary>Markdown content.</summary>
    Markdown
}

/// <summary>Editor color theme.</summary>
public enum RichTextTheme
{
    /// <summary>Follow host CSS / system preference (default).</summary>
    System,
    Light,
    Dark
}

/// <summary>Text direction for the editor or a block.</summary>
public enum TextDirection
{
    Ltr,
    Rtl
}

/// <summary>An error surfaced by the editor (e.g. invalid URL, failed upload, invalid HTML).</summary>
/// <param name="Code">Stable error code, e.g. "invalid-url".</param>
/// <param name="Message">Human-readable description.</param>
public sealed record RichTextError(string Code, string Message);
