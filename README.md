# BlazorRichTextEditor

**A native rich text editor component for Blazor** - a drop-in WYSIWYG editor in the
spirit of Quill and TinyMCE. Toolbar plus editable surface, two-way bound to an HTML
string, with all component logic in C# and a deliberately thin JavaScript bridge for the
browser-only concerns (`contenteditable` events, formatting commands, selection).

```razor
@using BlazorRichTextEditor

<BlazorRichTextEditor @bind-Value="html" Placeholder="Write something..." />

@code {
    private string html = "<p>Hello <strong>world</strong>.</p>";
}
```

## Features

- Formatting toolbar: bold, italic, underline, strikethrough; H1–H3 / paragraph;
  bullet and numbered lists; blockquote and code block; links and unlink; alignment;
  undo/redo; clear formatting.
- Two-way binding of HTML via `@bind-Value`.
- Live toolbar state (active buttons reflect the cursor's formatting).
- Read-only mode, placeholder text, configurable toolbar groups (`BlazorRichTextEditorToolbar` flags),
  height, and theming hooks.
- Paste cleanup (strips scripts and event-handler attributes) as a first line of defense.
- Imperative API: `FocusAsync()`, `GetHtmlAsync()`, `ExecuteCommandAsync(...)`.

## Setup

1. Reference the `BlazorRichTextEditor` project/package.
2. Add the stylesheet to your host page (`App.razor`):
   ```html
   <link rel="stylesheet" href="_content/BlazorRichTextEditor/blazorRichTextEditor.css" />
   ```
3. Use the component inside an **interactive** render mode (Server or WebAssembly).

## Security

`contenteditable` and pasted content are untrusted input. The component strips obvious
script vectors on paste, but you should still **sanitize the emitted HTML server-side**
(e.g. with HtmlSanitizer) before storing or redisplaying it.

See [`docs/BLAZOR_RICH_TEXT_EDITOR.md`](docs/BLAZOR_RICH_TEXT_EDITOR.md) for the design study and architecture.

## Projects

```
src/
  BlazorRichTextEditor/        The editor component (Razor Class Library + thin JS bridge)
  BlazorRichTextEditor.Demo/   Sample app showcasing the editor and @bind-Value
docs/BLAZOR_RICH_TEXT_EDITOR.md              Design study for the component
```

## Running the demo

```
dotnet run --project src/BlazorRichTextEditor.Demo
```

## License

MIT (intended).
