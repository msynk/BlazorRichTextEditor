using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

// Phase 2: table insertion and structural editing.
public partial class BlazorRichTextEditor
{
    private async Task InsertTableAsync(int rows, int cols)
    {
        if (ReadOnly || _module is null) return;
        if (rows < 1 || rows > 50 || cols < 1 || cols > 50)
        {
            await RaiseErrorAsync(new RichTextError("invalid-table", "Tables must be between 1 and 50 rows/columns."));
            return;
        }
        await _module.InvokeVoidAsync("insertTable", _editor, rows, cols);
    }

    private async Task TableOpAsync(string op)
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("tableOp", _editor, op);
    }
}
