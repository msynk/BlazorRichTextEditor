namespace BlazorRichTextEditor;

/// <summary>
/// Toolbar button groups. Combine with bitwise OR to choose which groups appear, or use
/// <see cref="All"/> for the default full toolbar.
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
    All = History | BlockFormat | Inline | Lists | Blocks | Link | Alignment | Clear
}

/// <summary>
/// Snapshot of the current selection's formatting, reported by the JS bridge and used to
/// highlight active toolbar buttons. Property names map (case-insensitively) to the JS
/// state object.
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
}
