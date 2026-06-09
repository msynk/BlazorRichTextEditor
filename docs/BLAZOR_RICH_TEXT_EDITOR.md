# BlazorRichTextEditor — a practical rich text editor component for Blazor

A drop-in WYSIWYG editor in the spirit of Quill and TinyMCE: a toolbar plus an editable
surface, two-way bound to an HTML string. Built for Blazor, usable in one line.

```razor
<BlazorRichTextEditor @bind-Value="html" Placeholder="Write something..." />
```

---

## 1. Goal

Give Blazor developers an editor they can actually drop into a page and use:

- A formatting toolbar (bold, italic, underline, strikethrough, headings, lists,
  blockquote, code, links, alignment, undo/redo, clear formatting).
- A `contenteditable` editing surface.
- Two-way binding of the HTML content via `@bind-Value`.
- Live toolbar state (active buttons reflect the cursor's current formatting).
- Read-only mode, placeholder text, and easy theming.

This is a *component*, not a research engine. It favors working behavior and a small
footprint over a custom document model.

## 2. Why a JS interop layer is unavoidable (and that's fine)

Rich text editing in the browser is only possible through APIs that are exposed to
JavaScript, not to .NET:

- `contenteditable` and the **Selection / Range** APIs.
- Clipboard and `paste` handling.
- The editing commands that apply formatting to a selection.

A truly "no JavaScript" Blazor editor is therefore not possible. Even commercial Blazor
editors and the popular `Blazored.TextEditor` (a Quill wrapper) rely on JS underneath.

BlazorRichTextEditor keeps the JS deliberately **thin and focused** (Microsoft's own JS interop
guidance: use small, focused interop in specific spots). All component logic — state,
binding, toolbar, configuration — lives in C#. JavaScript only:

1. wires DOM events (`input`, `selectionchange`, `paste`, `blur`) and forwards them to
   .NET, and
2. applies formatting commands and reports the current selection's formatting state.

## 3. Formatting: `execCommand` today, swappable tomorrow

The browser's built-in `document.execCommand` (bold, italic, formatBlock,
insertOrderedList, createLink, …) is **deprecated but still universally supported**, and
it is exactly what lightweight editors still use to mutate `contenteditable`. We use it
because it works everywhere today, and we **isolate it behind a single JS module** so the
implementation can later be replaced with a Selection/Range-based engine without touching
the C# component or its public API.

## 4. Architecture

```
┌──────────────────────── BlazorRichTextEditor (C#) ─────────────┐
│  Parameters: Value/ValueChanged (@bind), Placeholder, ReadOnly, │
│              ShowToolbar, ToolbarItems, Height, CssClass         │
│  State: current HTML, active formatting states for the toolbar  │
│  [JSInvokable] OnContentChanged(html)  ← input events           │
│  [JSInvokable] OnSelectionChanged(state) ← selectionchange      │
└───────────────▲───────────────────────────────┬────────────────┘
                │ IJSObjectReference (ESM module) │ DotNetObjectReference
┌───────────────┴───────────────────────────────▼────────────────┐
│  blazorRichTextEditor.js (thin):                               │
│   initialize / setHtml / getHtml / exec / queryState / dispose   │
│   listeners: input (debounced) · selectionchange · paste · blur  │
└──────────────────────────── contenteditable div ────────────────┘
```

### Content sync rules (avoiding caret jumps)

- **User types →** JS `input` (debounced) → `OnContentChanged(html)` → update internal
  value → raise `ValueChanged`. The component does **not** push the HTML back into the DOM
  in this direction (that would reset the caret).
- **Parent sets `Value` →** only when the incoming value differs from what the editor
  currently holds do we call JS `setHtml`, so external/programmatic changes are reflected
  without disturbing in-progress typing.

### Render-mode safety

JS interop only runs after the first interactive render (`OnAfterRenderAsync`), so the
component is safe under static server-side prerendering.

## 5. Security

- `contenteditable` and pasted content are **untrusted input**. On paste we strip script
  and event-handler attributes in JS as a first line of defense.
- The HTML the component emits should be **sanitized server-side** (e.g. with HtmlSanitizer)
  before storage or redisplay. The component documents this clearly rather than implying
  its output is safe by default.
- Per Microsoft guidance, be cautious feeding JS-interop-sourced data back into
  authenticated interactive server components.

## 6. Public API (target)

| Member | Purpose |
|---|---|
| `Value` / `ValueChanged` | `@bind-Value` the HTML content |
| `Placeholder` | Placeholder shown when empty |
| `ReadOnly` | Disable editing |
| `ShowToolbar` | Toggle the toolbar |
| `Toolbar` (enum flags) | Choose which button groups appear |
| `Height` / `CssClass` / `Style` | Sizing and theming |
| `OnFocus` / `OnBlur` | Focus event callbacks |
| `FocusAsync()` / `GetHtmlAsync()` / `ExecuteCommandAsync(...)` | Imperative API |

## 7. Roadmap for the component

1. **MVP (this phase):** toolbar, contenteditable, `@bind-Value`, live toolbar state,
   links, lists, headings, blockquote, code, undo/redo, clear formatting, placeholder,
   read-only, paste cleanup. Demo app.
2. Image insertion (URL + upload callback), text/background color, tables.
3. Markdown shortcuts and a slash menu.
4. Pluggable sanitizer integration and a Selection/Range-based formatting engine to
   replace `execCommand`.
5. Accessibility pass (ARIA roles, keyboard operability) and theming tokens.
