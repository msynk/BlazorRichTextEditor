using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorRichTextEditor;

// Toolbar render pipeline. Groups are rendered in a computed order (default = the original
// order). Custom items and host-specified ordering are layered over this seam.
public partial class BlazorRichTextEditor
{
    /// <summary>Custom toolbar items and ordering. Null uses the default group order.</summary>
    [Parameter] public RichTextToolbarConfig? ToolbarConfig { get; set; }

    private ElementReference _toolbar;
    // Stable identifiers for the built-in groups, in default display order.
    private static readonly (string Id, BlazorRichTextEditorToolbar Flag)[] DefaultGroupOrder =
    {
        ("history",   BlazorRichTextEditorToolbar.History),
        ("blockformat", BlazorRichTextEditorToolbar.BlockFormat),
        ("font",      BlazorRichTextEditorToolbar.Font),
        ("inline",    BlazorRichTextEditorToolbar.Inline),
        ("color",     BlazorRichTextEditorToolbar.Color),
        ("script",    BlazorRichTextEditorToolbar.Script),
        ("lists",     BlazorRichTextEditorToolbar.Lists),
        ("indent",    BlazorRichTextEditorToolbar.Indent),
        ("blocks",    BlazorRichTextEditorToolbar.Blocks),
        ("link",      BlazorRichTextEditorToolbar.Link),
        ("media",     BlazorRichTextEditorToolbar.Media),
        ("image",     BlazorRichTextEditorToolbar.Image),
        ("table",     BlazorRichTextEditorToolbar.Table),
        ("rule",      BlazorRichTextEditorToolbar.Rule),
        ("alignment", BlazorRichTextEditorToolbar.Alignment),
        ("direction", BlazorRichTextEditorToolbar.Direction),
        ("emoji",     BlazorRichTextEditorToolbar.Emoji),
        ("find",      BlazorRichTextEditorToolbar.Find),
        ("source",    BlazorRichTextEditorToolbar.Source),
        ("fullscreen", BlazorRichTextEditorToolbar.FullScreen),
        ("clear",     BlazorRichTextEditorToolbar.Clear),
    };

    /// <summary>
    /// The ordered list of toolbar entry ids to render. Built-in group ids are included only
    /// when their flag is enabled; custom item ids are interleaved per ToolbarConfig.
    /// </summary>
    private IEnumerable<string> OrderedToolbarIds()
    {
        var enabledGroups = DefaultGroupOrder.Where(g => Has(g.Flag)).Select(g => g.Id).ToList();
        var customIds = ToolbarConfig?.CustomItems?.Take(50).Select(i => i.Id).ToList() ?? new List<string>();

        if (ToolbarConfig?.Order is { Count: > 0 } order)
        {
            var known = new HashSet<string>(enabledGroups.Concat(customIds), StringComparer.OrdinalIgnoreCase);
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Ordered entries first (skip unknown ids — Req 27.6).
            foreach (var id in order)
                if (known.Contains(id) && emitted.Add(id))
                    yield return id;
            // Append omitted entries in default order (Req 27.5).
            foreach (var id in enabledGroups.Concat(customIds))
                if (emitted.Add(id))
                    yield return id;
            yield break;
        }

        foreach (var id in enabledGroups) yield return id;
        foreach (var id in customIds) yield return id;
    }

    private void RenderCustomItem(RenderTreeBuilder builder, string id)
    {
        var item = ToolbarConfig?.CustomItems?.FirstOrDefault(i =>
            string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "blazor-rte-group");
        builder.OpenElement(2, "button");
        builder.AddAttribute(3, "type", "button");
        builder.AddAttribute(4, "class", "blazor-rte-btn");
        builder.AddAttribute(5, "title", item.AriaLabel);
        builder.AddAttribute(6, "aria-label", item.AriaLabel);
        builder.AddAttribute(7, "disabled", ControlsDisabled);
        builder.AddAttribute(9, "onclick", EventCallback.Factory.Create(this, () => InvokeCustomItemAsync(item)));
        if (item.Icon is not null) builder.AddContent(10, item.Icon);
        else builder.AddContent(11, item.Label ?? item.Id);
        builder.CloseElement();
        builder.CloseElement();
    }

    private async Task InvokeCustomItemAsync(RichTextToolbarItem item)
    {
        try
        {
            await item.OnActivate(this);
        }
        catch (Exception ex)
        {
            await RaiseErrorAsync(new RichTextError("custom-action-failed", $"Toolbar action '{item.Id}' failed: {ex.Message}"));
        }
    }
}
