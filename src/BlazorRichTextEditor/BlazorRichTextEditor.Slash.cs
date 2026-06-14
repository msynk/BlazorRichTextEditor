using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 4: slash command menu. Markdown shortcuts are handled in the JS bridge; the slash
// trigger is detected there and surfaced here so the menu and command list live in C#.
public partial class BlazorRichTextEditor
{
    private bool _showSlash;
    private string _slashFilter = "";

    private readonly record struct SlashCommand(string Label, string Command);

    private static readonly SlashCommand[] SlashCommands =
    {
        new("Heading 1", "h1"),
        new("Heading 2", "h2"),
        new("Heading 3", "h3"),
        new("Paragraph", "p"),
        new("Bulleted list", "insertUnorderedList"),
        new("Numbered list", "insertOrderedList"),
        new("Quote", "blockquote"),
        new("Code block", "pre"),
    };

    /// <summary>Called by the bridge when the user types the slash trigger.</summary>
    [JSInvokable]
    public void OnSlashTrigger()
    {
        _slashFilter = "";
        _showSlash = true;
        StateHasChanged();
    }

    private IEnumerable<SlashCommand> FilteredSlash()
    {
        var term = _slashFilter?.Trim();
        if (string.IsNullOrEmpty(term)) return SlashCommands;
        return SlashCommands.Where(c => c.Label.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private void CloseSlash()
    {
        _showSlash = false;
        _slashFilter = "";
    }

    private async Task ApplySlashAsync(string command)
    {
        _showSlash = false;
        _slashFilter = "";
        if (_module is not null)
            await _module.InvokeVoidAsync("applySlashCommand", _editor, command);
    }
}
