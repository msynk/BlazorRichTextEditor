using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Cross-cutting hooks established by the foundation phase. Each is expanded by its
// owning feature partial in later phases. Keeping them here lets the core lifecycle
// reference stable seams without depending on a specific feature's presence.
public partial class BlazorRichTextEditor
{
    // --- Character/word count (Phase 3) ---
    /// <summary>Show the character/word count footer.</summary>
    [Parameter] public bool ShowCount { get; set; }

    // --- Paste behavior (Phase 4) ---
    /// <summary>When true, pasted content is inserted as plain text.</summary>
    [Parameter] public bool PasteAsPlainText { get; set; }

    // --- Keyboard shortcuts (Phase 3) ---
    /// <summary>
    /// Custom key-combo → command map, merged over the built-in defaults. Keys use the form
    /// "ctrl+b", "ctrl+shift+k" (use "ctrl" for the primary modifier on all platforms).
    /// </summary>
    [Parameter] public IReadOnlyDictionary<string, string>? KeyboardShortcuts { get; set; }

    private static readonly Dictionary<string, string> DefaultShortcuts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl+b"] = "bold",
        ["ctrl+i"] = "italic",
        ["ctrl+u"] = "underline",
        ["ctrl+z"] = "undo",
        ["ctrl+y"] = "redo",
        ["ctrl+shift+z"] = "redo"
    };

    // --- Keyboard shortcuts (Phase 3) ---
    /// <summary>
    /// Invoked by the JS bridge for Ctrl/Cmd keystrokes. Returns true when handled so the
    /// bridge can suppress the browser default.
    /// </summary>
    [JSInvokable]
    public async Task<bool> OnShortcut(string key, bool ctrl, bool shift, bool alt)
    {
        if (ReadOnly || _module is null) return false;

        var combo = BuildComboKey(key, ctrl, shift, alt);
        string? command = null;
        if (KeyboardShortcuts is not null && KeyboardShortcuts.TryGetValue(combo, out var custom))
            command = custom;                                   // custom wins (Req 21.3)
        else if (DefaultShortcuts.TryGetValue(combo, out var def))
            command = def;

        if (command is null) return false;

        if (!IsKnownCommand(command))
        {
            await RaiseErrorAsync(new RichTextError("unknown-shortcut", $"Shortcut command '{command}' is not recognized."));
            return false;
        }

        await ExecAsync(command);
        return true;
    }

    private static string BuildComboKey(string key, bool ctrl, bool shift, bool alt)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("ctrl");
        if (shift) parts.Add("shift");
        if (alt) parts.Add("alt");
        parts.Add(key.ToLowerInvariant());
        return string.Join('+', parts);
    }

    private static readonly HashSet<string> KnownCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "bold", "italic", "underline", "strikeThrough", "undo", "redo",
        "insertOrderedList", "insertUnorderedList", "justifyLeft", "justifyCenter",
        "justifyRight", "justifyFull", "indent", "outdent", "subscript", "superscript",
        "removeFormat", "unlink", "insertHorizontalRule"
    };

    private static bool IsKnownCommand(string command) => KnownCommands.Contains(command);
}
