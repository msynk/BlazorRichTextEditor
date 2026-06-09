// BlazorRichTextEditor - thin JS bridge.
// Owns nothing but DOM events, formatting commands, and selection. All component
// logic lives in C#. execCommand is isolated here so it can be swapped later.

export function initialize(editor, dotNetRef, options) {
    if (!editor) return;
    options = options || {};
    editor._dotNetRef = dotNetRef;
    editor._debounce = options.debounce ?? 200;
    let timer = null;

    const notify = () => {
        if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnContentChanged', editor.innerHTML);
    };

    editor._onInput = () => {
        if (timer) clearTimeout(timer);
        timer = setTimeout(notify, editor._debounce);
    };
    editor.addEventListener('input', editor._onInput);

    editor._onBlur = () => {
        if (timer) { clearTimeout(timer); timer = null; }
        notify();
        if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnBlurred');
    };
    editor.addEventListener('blur', editor._onBlur);

    editor._onFocus = () => {
        if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnFocused');
    };
    editor.addEventListener('focus', editor._onFocus);

    editor._onSelection = () => {
        const sel = document.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        if (editor.contains(sel.anchorNode)) {
            editor._range = sel.getRangeAt(0).cloneRange();
            reportState(editor);
        }
    };
    document.addEventListener('selectionchange', editor._onSelection);

    editor._onPaste = (e) => {
        const cb = e.clipboardData;
        if (!cb) return;
        e.preventDefault();
        const html = cb.getData('text/html');
        const text = cb.getData('text/plain');
        const toInsert = html ? sanitize(html) : escapeHtml(text).replace(/\r?\n/g, '<br>');
        document.execCommand('insertHTML', false, toInsert);
        notify();
    };
    editor.addEventListener('paste', editor._onPaste);
}

export function setHtml(editor, html) {
    if (editor && editor.innerHTML !== (html ?? '')) editor.innerHTML = html ?? '';
}

export function getHtml(editor) {
    return editor ? editor.innerHTML : '';
}

export function focus(editor) {
    editor?.focus();
}

export function exec(editor, command, value) {
    if (!editor) return '';
    editor.focus();
    restoreSelection(editor);
    if (command === 'formatBlock' && value && value[0] !== '<') value = '<' + value + '>';
    try { document.execCommand('styleWithCSS', false, false); } catch { /* ignore */ }
    try { document.execCommand(command, false, value ?? null); } catch { /* ignore */ }
    afterChange(editor);
    return editor.innerHTML;
}

export function createLink(editor, url) {
    if (!editor || !url) return;
    editor.focus();
    restoreSelection(editor);
    // If the selection is collapsed, insert the URL as the link text.
    const sel = document.getSelection();
    if (sel && sel.isCollapsed) {
        document.execCommand('insertHTML', false,
            `<a href="${escapeAttr(url)}">${escapeHtml(url)}</a>`);
    } else {
        document.execCommand('createLink', false, url);
    }
    afterChange(editor);
}

export function dispose(editor) {
    if (!editor) return;
    editor.removeEventListener('input', editor._onInput);
    editor.removeEventListener('blur', editor._onBlur);
    editor.removeEventListener('focus', editor._onFocus);
    editor.removeEventListener('paste', editor._onPaste);
    document.removeEventListener('selectionchange', editor._onSelection);
    editor._dotNetRef = null;
    editor._range = null;
}

// ---- helpers ----

function afterChange(editor) {
    if (!editor._dotNetRef) return;
    editor._dotNetRef.invokeMethodAsync('OnContentChanged', editor.innerHTML);
    reportState(editor);
}

function reportState(editor) {
    if (!editor._dotNetRef) return;
    editor._dotNetRef.invokeMethodAsync('OnSelectionChanged', currentState());
}

function currentState() {
    const q = (c) => { try { return document.queryCommandState(c); } catch { return false; } };
    let block = '';
    try { block = (document.queryCommandValue('formatBlock') || '').toString().toLowerCase(); } catch { /* ignore */ }
    return {
        bold: q('bold'),
        italic: q('italic'),
        underline: q('underline'),
        strikeThrough: q('strikeThrough'),
        orderedList: q('insertOrderedList'),
        unorderedList: q('insertUnorderedList'),
        justifyLeft: q('justifyLeft'),
        justifyCenter: q('justifyCenter'),
        justifyRight: q('justifyRight'),
        block: block
    };
}

function restoreSelection(editor) {
    const r = editor._range;
    if (!r) return;
    const sel = document.getSelection();
    if (!sel) return;
    sel.removeAllRanges();
    sel.addRange(r);
}

function sanitize(html) {
    const tpl = document.createElement('template');
    tpl.innerHTML = html;
    tpl.content.querySelectorAll('script,style,iframe,object,embed,link,meta,title,head').forEach(n => n.remove());
    tpl.content.querySelectorAll('*').forEach(el => {
        for (const attr of [...el.attributes]) {
            const name = attr.name.toLowerCase();
            if (name.startsWith('on')) el.removeAttribute(attr.name);
            else if ((name === 'href' || name === 'src') && /^\s*javascript:/i.test(attr.value)) el.removeAttribute(attr.name);
        }
    });
    return tpl.innerHTML;
}

function escapeHtml(s) {
    const d = document.createElement('div');
    d.textContent = s ?? '';
    return d.innerHTML;
}

function escapeAttr(s) {
    return (s ?? '').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
