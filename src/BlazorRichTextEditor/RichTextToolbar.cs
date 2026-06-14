using Microsoft.AspNetCore.Components;

namespace BlazorRichTextEditor;

/// <summary>
/// Configures toolbar ordering and custom items. Provide via the <c>ToolbarConfig</c> parameter.
/// </summary>
public sealed class RichTextToolbarConfig
{
    /// <summary>
    /// Explicit ordering of toolbar entry ids (built-in group ids and custom item ids).
    /// Unknown ids are skipped; omitted enabled entries are appended in default order.
    /// Built-in group ids: history, blockformat, font, inline, color, script, lists, indent,
    /// blocks, link, media, image, table, rule, alignment, direction, emoji, find, source,
    /// fullscreen, clear.
    /// </summary>
    public IReadOnlyList<string>? Order { get; init; }

    /// <summary>Custom toolbar items (max 50 are rendered).</summary>
    public IReadOnlyList<RichTextToolbarItem>? CustomItems { get; init; }
}

/// <summary>A custom toolbar button supplied by the host.</summary>
public sealed class RichTextToolbarItem
{
    /// <summary>Unique id used for ordering and lookup.</summary>
    public required string Id { get; init; }

    /// <summary>Text label shown when no icon is provided.</summary>
    public string? Label { get; init; }

    /// <summary>Optional icon content.</summary>
    public RenderFragment? Icon { get; init; }

    /// <summary>Non-empty accessible label / tooltip.</summary>
    public required string AriaLabel { get; init; }

    /// <summary>Action invoked when the item is activated; receives the editor instance.</summary>
    public required Func<BlazorRichTextEditor, Task> OnActivate { get; init; }
}
