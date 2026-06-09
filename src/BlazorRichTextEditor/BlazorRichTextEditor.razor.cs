using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorRichTextEditor;

/// <summary>
/// A drop-in WYSIWYG rich text editor for Blazor. Two-way bind the HTML content with
/// <c>@bind-Value</c>. All component logic lives in C#; a thin JS module handles the
/// browser-only concerns (contenteditable events, formatting commands, selection).
/// </summary>
public partial class BlazorRichTextEditor : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The HTML content of the editor. Use with <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the content changes.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Placeholder text shown while the editor is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>When true, the editor is not editable.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Whether the formatting toolbar is shown.</summary>
    [Parameter] public bool ShowToolbar { get; set; } = true;

    /// <summary>Which toolbar groups to display.</summary>
    [Parameter] public BlazorRichTextEditorToolbar Toolbar { get; set; } = BlazorRichTextEditorToolbar.All;

    /// <summary>Minimum height of the editing surface (any CSS length).</summary>
    [Parameter] public string Height { get; set; } = "300px";

    /// <summary>Extra CSS class applied to the editor root.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>Inline style applied to the editor root.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Debounce window (ms) for content-change notifications while typing.</summary>
    [Parameter] public int DebounceMs { get; set; } = 200;

    /// <summary>Raised when the editor gains focus.</summary>
    [Parameter] public EventCallback OnFocus { get; set; }

    /// <summary>Raised when the editor loses focus.</summary>
    [Parameter] public EventCallback OnBlur { get; set; }

    private ElementReference _editor;
    private IJSObjectReference? _module;
    private DotNetObjectReference<BlazorRichTextEditor>? _dotNetRef;
    private BlazorRichTextEditorSelectionState _state = new();
    private string _currentHtml = "";
    private bool _initialized;
    private bool _isEmpty = true;

    private bool _showLinkInput;
    private string _linkUrl = "";

    private bool Has(BlazorRichTextEditorToolbar group) => Toolbar.HasFlag(group);

    protected override void OnParametersSet()
    {
        _isEmpty = IsContentEmpty(Value);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorRichTextEditor/blazorRichTextEditor.js");
            _dotNetRef = DotNetObjectReference.Create(this);
            _currentHtml = Value ?? "";
            await _module.InvokeVoidAsync("initialize", _editor, _dotNetRef,
                new { debounce = DebounceMs });
            if (!string.IsNullOrEmpty(_currentHtml))
                await _module.InvokeVoidAsync("setHtml", _editor, _currentHtml);
            _initialized = true;
        }
        else if (_initialized && _module is not null && (Value ?? "") != _currentHtml)
        {
            // External/programmatic change to Value: reflect it without disturbing typing.
            _currentHtml = Value ?? "";
            await _module.InvokeVoidAsync("setHtml", _editor, _currentHtml);
        }
    }

    // ---- callbacks from JS ----

    [JSInvokable]
    public async Task OnContentChanged(string html)
    {
        _currentHtml = html;
        var empty = IsContentEmpty(html);
        if (empty != _isEmpty)
        {
            _isEmpty = empty;
            StateHasChanged();
        }
        await ValueChanged.InvokeAsync(html);
    }

    [JSInvokable]
    public void OnSelectionChanged(BlazorRichTextEditorSelectionState state)
    {
        _state = state;
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnFocused() => await OnFocus.InvokeAsync();

    [JSInvokable]
    public async Task OnBlurred() => await OnBlur.InvokeAsync();

    // ---- commands ----

    private async Task ExecAsync(string command, string? value = null)
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("exec", _editor, command, value);
    }

    private Task UndoAsync() => ExecAsync("undo");
    private Task RedoAsync() => ExecAsync("redo");

    private Task OnBlockFormatChanged(ChangeEventArgs e)
        => ExecAsync("formatBlock", e.Value?.ToString() ?? "p");

    private Task FormatBlockToggleAsync(string tag)
        => ExecAsync("formatBlock", _state.Block == tag ? "p" : tag);

    private async Task ClearFormattingAsync()
    {
        if (ReadOnly || _module is null) return;
        await _module.InvokeVoidAsync("exec", _editor, "removeFormat", null);
        await _module.InvokeVoidAsync("exec", _editor, "formatBlock", "p");
    }

    private void ToggleLinkInput()
    {
        _showLinkInput = !_showLinkInput;
        if (!_showLinkInput) _linkUrl = "";
    }

    private async Task ApplyLinkAsync()
    {
        if (!string.IsNullOrWhiteSpace(_linkUrl) && _module is not null)
            await _module.InvokeVoidAsync("createLink", _editor, _linkUrl.Trim());
        _showLinkInput = false;
        _linkUrl = "";
    }

    private async Task OnLinkKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ApplyLinkAsync();
        else if (e.Key == "Escape") ToggleLinkInput();
    }

    // ---- imperative API ----

    /// <summary>Moves keyboard focus into the editor.</summary>
    public async Task FocusAsync()
    {
        if (_module is not null) await _module.InvokeVoidAsync("focus", _editor);
    }

    /// <summary>Returns the current HTML content.</summary>
    public async Task<string> GetHtmlAsync()
        => _module is null ? _currentHtml : await _module.InvokeAsync<string>("getHtml", _editor);

    /// <summary>Runs a raw editing command against the editor.</summary>
    public Task ExecuteCommandAsync(string command, string? value = null) => ExecAsync(command, value);

    // ---- helpers ----

    private static bool IsContentEmpty(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return true;
        var stripped = html
            .Replace("<br>", "").Replace("<br/>", "").Replace("<br />", "")
            .Replace("&nbsp;", "").Replace("\u00a0", "");
        var inText = false;
        var sawText = false;
        foreach (var c in stripped)
        {
            if (c == '<') inText = true;
            else if (c == '>') inText = false;
            else if (!inText && !char.IsWhiteSpace(c)) { sawText = true; break; }
        }
        return !sawText;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _editor);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { /* circuit gone; nothing to clean up */ }
        _dotNetRef?.Dispose();
    }
}
