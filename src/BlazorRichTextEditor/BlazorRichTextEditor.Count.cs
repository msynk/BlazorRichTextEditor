using Microsoft.AspNetCore.Components;

namespace BlazorRichTextEditor;

// Phase 3: character/word count and MaxLength enforcement. The count values come from the
// content facts reported by the bridge; enforcement happens in the bridge on input/paste.
public partial class BlazorRichTextEditor
{
    /// <summary>Maximum plain-text character count. Null means unlimited.</summary>
    [Parameter] public int? MaxLength { get; set; }
}
