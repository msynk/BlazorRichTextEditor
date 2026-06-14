// BlazorRichTextEditor - thin JS bridge.
// Owns nothing but DOM events, formatting commands, and selection. All component
// logic lives in C#.
//
// The single command seam:  every formatting / insertion operation flows through
// `dispatch(editor, command, args)`, which delegates to the active engine. Today the
// engine is `createExecCommandEngine()` (document.execCommand). A future Selection/Range
// engine can be swapped in here without touching C# call sites or the rest of the bridge.

// ---------------------------------------------------------------------------
// Engine: the ONLY place document.execCommand is invoked.
// ---------------------------------------------------------------------------
function createExecCommandEngine() {
    const run = (editor, command, args) => {
        editor.focus();
        restoreSelection(editor);
        try { document.execCommand('styleWithCSS', false, false); } catch { /* ignore */ }

        switch (command) {
            // --- block format (formatBlock needs <tag> wrapping) ---
            case 'formatBlock': {
                let v = args?.value ?? 'p';
                if (v && v[0] !== '<') v = '<' + v + '>';
                return execNative(editor, 'formatBlock', v);
            }
            // --- color ---
            case 'foreColor':
                return execNative(editor, 'foreColor', args?.value);
            case 'backColor':
                // hiliteColor is the standard; backColor is the fallback for some engines.
                return execNative(editor, 'hiliteColor', args?.value) ||
                       execNative(editor, 'backColor', args?.value);
            // --- font ---
            case 'fontName':
                return execNative(editor, 'fontName', args?.value);
            case 'fontSize':
                return applyFontSize(editor, args?.value);
            // --- structural inserts that execCommand can express ---
            case 'insertImage':
                return insertNodeHtml(editor, args?.html);
            case 'insertHtml':
                return execNative(editor, 'insertHTML', args?.html);
            case 'insertHorizontalRule':
                return insertHorizontalRule(editor);
            case 'createLink':
                return createLinkImpl(editor, args?.value);
            case 'insertTable':
                return insertNodeHtml(editor, args?.html);
            case 'insertMedia':
                return insertNodeHtml(editor, args?.html);
            // --- everything else maps 1:1 to an execCommand id ---
            default:
                return execNative(editor, command, args?.value ?? null);
        }
    };
    return { run };
}

function execNative(editor, command, value) {
    try { return document.execCommand(command, false, value ?? null); }
    catch { return false; }
}

// Normalize execCommand fontSize (1-7) onto a real size by post-processing the
// <font size> it produces into an inline style when a css length is supplied.
function applyFontSize(editor, value) {
    if (!value) return false;
    // Use a sentinel size, then rewrite the produced font[size] elements.
    execNative(editor, 'fontSize', '7');
    editor.querySelectorAll('font[size="7"]').forEach(f => {
        f.removeAttribute('size');
        f.style.fontSize = value;
    });
    return true;
}

let _engine = createExecCommandEngine();

// The single seam. Returns the engine result; callers read editor.innerHTML separately.
export function dispatch(editor, command, args) {
    if (!editor) return false;
    try {
        return _engine.run(editor, command, args || {});
    } catch (err) {
        // Report failure to C#; leave content as-is (Req 2.6 / 6.4).
        if (editor._dotNetRef) {
            editor._dotNetRef.invokeMethodAsync('OnCommandError', String(command), String(err?.message ?? err));
        }
        return false;
    }
}

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------
export function initialize(editor, dotNetRef, options) {
    if (!editor) return;
    options = options || {};
    editor._dotNetRef = dotNetRef;
    editor._debounce = options.debounce ?? 200;
    editor._policy = options.policy ?? null;
    editor._hasUpload = options.hasUpload === true;
    editor._plainTextPaste = options.plainTextPaste === true;
    editor._maxLength = (typeof options.maxLength === 'number') ? options.maxLength : null;
    let timer = null;

    const notify = () => {
        if (editor._dotNetRef)
            editor._dotNetRef.invokeMethodAsync('OnContentChanged', editor.innerHTML, computeFacts(editor));
    };
    editor._notify = notify;

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

    editor._onPaste = (e) => onPaste(editor, e);
    editor.addEventListener('paste', editor._onPaste);

    editor._onDrop = (e) => onDrop(editor, e);
    editor.addEventListener('drop', editor._onDrop);

    editor._onKeyDown = (e) => onKeyDown(editor, e);
    editor.addEventListener('keydown', editor._onKeyDown);

    editor._onBeforeInput = (e) => onBeforeInput(editor, e);
    editor.addEventListener('beforeinput', editor._onBeforeInput);

    editor._onInputMd = (e) => onInputMarkdown(editor, e);
    editor.addEventListener('input', editor._onInputMd);

    enableImageResize(editor);
    enableTableResize(editor);
}

// Column resize by dragging near a cell's right border (Req 13.6).
function enableTableResize(editor) {
    if (editor._tableResizeWired) return;
    editor._tableResizeWired = true;
    editor.addEventListener('mousedown', (e) => {
        const cell = e.target.closest && e.target.closest('td,th');
        if (!cell) return;
        const rect = cell.getBoundingClientRect();
        if (e.clientX < rect.right - 6) return;   // only near the right edge
        e.preventDefault();
        const startX = e.clientX;
        const startW = rect.width;
        const onMove = (m) => {
            const w = Math.max(1, Math.round(startW + (m.clientX - startX)));
            cell.style.width = `${w}px`;
        };
        const onUp = () => {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            const w = Math.max(1, Math.round(cell.getBoundingClientRect().width));
            cell.setAttribute('width', String(w));
            if (editor._notify) editor._notify();
        };
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    });
}

// Markdown shortcuts (Req 22.1) + slash trigger (Req 22.2): run after input mutates the DOM.
function onInputMarkdown(editor, e) {
    if (editor._mdBusy) return;
    const block = currentBlock(editor);
    if (!block) return;
    const text = block.textContent || '';

    // Slash command trigger at the very start of an empty-ish block.
    if (e.inputType === 'insertText' && e.data === '/' && text === '/') {
        if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnSlashTrigger');
        return;
    }

    if (e.inputType !== 'insertText' || e.data !== ' ') return;
    const map = {
        '#': 'h1', '##': 'h2', '###': 'h3',
        '>': 'blockquote'
    };
    const marker = text.trim();
    if (map[marker]) {
        editor._mdBusy = true;
        clearBlockText(block);
        dispatch(editor, 'formatBlock', { value: map[marker] });
        editor._mdBusy = false;
        afterChange(editor);
    } else if (marker === '-' || marker === '*') {
        editor._mdBusy = true;
        clearBlockText(block);
        dispatch(editor, 'insertUnorderedList', {});
        editor._mdBusy = false;
        afterChange(editor);
    } else if (marker === '1.') {
        editor._mdBusy = true;
        clearBlockText(block);
        dispatch(editor, 'insertOrderedList', {});
        editor._mdBusy = false;
        afterChange(editor);
    }
}

function currentBlock(editor) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    let node = sel.anchorNode;
    if (node && node.nodeType === 3) node = node.parentNode;
    while (node && node !== editor && getComputedStyle(node).display === 'inline') node = node.parentNode;
    return node && node !== editor ? node : null;
}

function clearBlockText(block) {
    block.textContent = '';
    const sel = document.getSelection();
    const range = document.createRange();
    range.selectNodeContents(block);
    range.collapse(true);
    sel.removeAllRanges();
    sel.addRange(range);
}

// Removes the leading "/" trigger then applies a slash-menu command.
export function applySlashCommand(editor, command) {
    const block = currentBlock(editor);
    if (block && (block.textContent || '').startsWith('/')) {
        block.textContent = block.textContent.slice(1);
    }
    if (['h1', 'h2', 'h3', 'p', 'blockquote', 'pre'].includes(command)) {
        dispatch(editor, 'formatBlock', { value: command });
    } else {
        dispatch(editor, command, {});
    }
    afterChange(editor);
}

export function dispose(editor) {
    if (!editor) return;
    editor.removeEventListener('input', editor._onInput);
    editor.removeEventListener('input', editor._onInputMd);
    editor.removeEventListener('blur', editor._onBlur);
    editor.removeEventListener('focus', editor._onFocus);
    editor.removeEventListener('paste', editor._onPaste);
    editor.removeEventListener('drop', editor._onDrop);
    editor.removeEventListener('keydown', editor._onKeyDown);
    editor.removeEventListener('beforeinput', editor._onBeforeInput);
    document.removeEventListener('selectionchange', editor._onSelection);
    removeResizeHandle(editor);
    editor._dotNetRef = null;
    editor._range = null;
}

// ---------------------------------------------------------------------------
// Content get/set
// ---------------------------------------------------------------------------
export function getHtml(editor) {
    return editor ? editor.innerHTML : '';
}

// Undo-safe set: when the surface is focused and already has content, route the
// replacement through the engine (insertHTML) so the native undo stack survives.
// On initial load / unfocused surface, assign directly (no history to preserve).
export function setHtml(editor, html) {
    if (!editor) return;
    const next = html ?? '';
    if (editor.innerHTML === next) return;

    const focused = document.activeElement === editor;
    const hasContent = editor.innerHTML.trim().length > 0;
    if (focused && hasContent) {
        const sel = document.getSelection();
        const range = document.createRange();
        range.selectNodeContents(editor);
        sel.removeAllRanges();
        sel.addRange(range);
        if (!execNative(editor, 'insertHTML', next)) {
            editor.innerHTML = next;
        }
    } else {
        editor.innerHTML = next;
    }
}

export function focus(editor) {
    editor?.focus();
}

export function setPolicy(editor, policy) {
    if (editor) editor._policy = policy ?? null;
}

// Sanitize an arbitrary HTML string against the active policy (used by source-view exit).
export function sanitizeHtml(editor, html) {
    return sanitize(editor, html ?? '');
}

// ---------------------------------------------------------------------------
// Command entry points used by C# (all route through dispatch)
// ---------------------------------------------------------------------------
export function exec(editor, command, value) {
    if (!editor) return '';
    dispatch(editor, command, { value });
    afterChange(editor);
    return editor.innerHTML;
}

export function execBlock(editor, tag) {
    if (!editor) return '';
    dispatch(editor, 'formatBlock', { value: tag });
    afterChange(editor);
    return editor.innerHTML;
}

export function createLink(editor, url) {
    if (!editor || !url) return;
    dispatch(editor, 'createLink', { value: url });
    afterChange(editor);
}

// ---------------------------------------------------------------------------
// Phase 1 feature commands
// ---------------------------------------------------------------------------
export function insertImageUrl(editor, url) {
    if (!editor || !url) return;
    dispatch(editor, 'insertImage', { html: `<img src="${escapeAttr(url)}" alt="">` });
    afterChange(editor);
}

export function applyColor(editor, kind, value) {
    if (!editor || !value) return;
    dispatch(editor, kind === 'back' ? 'backColor' : 'foreColor', { value });
    afterChange(editor);
}

export function applyFont(editor, kind, value) {
    if (!editor || !value) return;
    dispatch(editor, kind === 'size' ? 'fontSize' : 'fontName', { value });
    afterChange(editor);
}

export function setPlainTextPaste(editor, enabled) {
    if (editor) editor._plainTextPaste = enabled === true;
}

// ---------------------------------------------------------------------------
// Phase 2/4 feature commands
// ---------------------------------------------------------------------------
export function insertMedia(editor, html) {
    if (!editor || !html) return;
    dispatch(editor, 'insertMedia', { html });
    afterChange(editor);
}

export function insertText(editor, text) {
    if (!editor || !text) return;
    dispatch(editor, 'insertText', { value: text });
    afterChange(editor);
}

export function updateLink(editor, url) {
    if (!editor || !url) return;
    const a = linkAtSelection(editor);
    if (a) {
        a.setAttribute('href', url);
    } else {
        dispatch(editor, 'createLink', { value: url });
    }
    afterChange(editor);
}

export function insertTable(editor, rows, cols) {
    if (!editor) return;
    let html = '<table class="blazor-rte-table"><tbody>';
    for (let r = 0; r < rows; r++) {
        html += '<tr>';
        for (let c = 0; c < cols; c++) html += '<td><br></td>';
        html += '</tr>';
    }
    html += '</tbody></table><p><br></p>';
    dispatch(editor, 'insertHtml', { html });
    afterChange(editor);
}

export function tableOp(editor, op) {
    const cell = cellAtSelection(editor);
    if (!cell) return;
    const row = cell.parentElement;
    const table = cell.closest('table');
    if (!table || !row) return;
    const colIndex = [...row.children].indexOf(cell);

    switch (op) {
        case 'addRow': {
            const nr = document.createElement('tr');
            for (let i = 0; i < row.children.length; i++) {
                const td = document.createElement('td'); td.innerHTML = '<br>'; nr.appendChild(td);
            }
            row.after(nr);
            break;
        }
        case 'addCol': {
            for (const tr of table.querySelectorAll('tr')) {
                const td = document.createElement('td'); td.innerHTML = '<br>';
                const ref = tr.children[colIndex];
                if (ref) ref.after(td); else tr.appendChild(td);
            }
            break;
        }
        case 'delRow': {
            const rows = table.querySelectorAll('tr');
            if (rows.length <= 1) { table.remove(); } else { row.remove(); }
            break;
        }
        case 'delCol': {
            const firstRow = table.querySelector('tr');
            if (firstRow && firstRow.children.length <= 1) { table.remove(); }
            else { for (const tr of table.querySelectorAll('tr')) { const c = tr.children[colIndex]; if (c) c.remove(); } }
            break;
        }
        case 'merge': {
            mergeSelectedCells(editor, table);
            break;
        }
    }
    afterChange(editor);
}

function cellAtSelection(editor) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    let node = sel.anchorNode;
    while (node && node !== editor) {
        if (node.nodeType === 1 && (node.tagName === 'TD' || node.tagName === 'TH')) return node;
        node = node.parentNode;
    }
    return null;
}

function mergeSelectedCells(editor, table) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) return;
    const range = sel.getRangeAt(0);
    const cells = [...table.querySelectorAll('td,th')].filter(c => range.intersectsNode(c));
    if (cells.length < 2) return;
    const first = cells[0];
    first.setAttribute('colspan', String((parseInt(first.getAttribute('colspan') || '1')) + cells.length - 1));
    for (let i = 1; i < cells.length; i++) {
        if (cells[i].innerHTML && cells[i].innerHTML !== '<br>') first.innerHTML += ' ' + cells[i].innerHTML;
        cells[i].remove();
    }
}

// ---- find & replace ----
export function clearFind(editor) {
    if (!editor) return;
    editor.querySelectorAll('mark.blazor-rte-find').forEach(m => {
        const parent = m.parentNode;
        m.replaceWith(...m.childNodes);
        parent && parent.normalize();
    });
    editor._findIndex = -1;
}

export function find(editor, term, caseSensitive) {
    clearFind(editor);
    if (!term) return 0;
    const flags = caseSensitive ? 'g' : 'gi';
    const rx = new RegExp(escapeRegExp(term), flags);
    let count = 0;
    const walker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT, null);
    const textNodes = [];
    while (walker.nextNode()) textNodes.push(walker.currentNode);
    for (const tn of textNodes) {
        const text = tn.nodeValue;
        if (!rx.test(text)) continue;
        rx.lastIndex = 0;
        const frag = document.createDocumentFragment();
        let last = 0, m;
        while ((m = rx.exec(text)) !== null) {
            if (m.index > last) frag.appendChild(document.createTextNode(text.slice(last, m.index)));
            const mark = document.createElement('mark');
            mark.className = 'blazor-rte-find';
            mark.textContent = m[0];
            frag.appendChild(mark);
            last = m.index + m[0].length;
            count++;
            if (m[0].length === 0) rx.lastIndex++;
        }
        if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
        tn.replaceWith(frag);
    }
    editor._findIndex = count > 0 ? 0 : -1;
    return count;
}

export function replaceCurrent(editor, term, replacement, caseSensitive) {
    const marks = editor.querySelectorAll('mark.blazor-rte-find');
    if (marks.length === 0) return 0;
    const idx = Math.min(Math.max(editor._findIndex ?? 0, 0), marks.length - 1);
    const mark = marks[idx];
    mark.replaceWith(document.createTextNode(replacement ?? ''));
    editor.normalize();
    afterChange(editor);
    return find(editor, term, caseSensitive);
}

export function replaceAll(editor, term, replacement, caseSensitive) {
    clearFind(editor);
    if (!term) return 0;
    const flags = caseSensitive ? 'g' : 'gi';
    const rx = new RegExp(escapeRegExp(term), flags);
    let count = 0;
    const walker = document.createTreeWalker(editor, NodeFilter.SHOW_TEXT, null);
    const textNodes = [];
    while (walker.nextNode()) textNodes.push(walker.currentNode);
    for (const tn of textNodes) {
        const replaced = tn.nodeValue.replace(rx, () => { count++; return replacement ?? ''; });
        if (replaced !== tn.nodeValue) tn.nodeValue = replaced;
    }
    afterChange(editor);
    return count;
}

function escapeRegExp(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }

// ---- full screen / direction ----
export function setFullScreen(editor, on) {
    if (!editor) return;
    const root = editor.closest('.blazor-rte');
    if (!root) return;
    if (on) {
        if (root.requestFullscreen) {
            root.requestFullscreen().catch(() => {
                if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnClientError', 'fullscreen-denied', 'Full-screen mode was blocked by the browser.');
            });
        }
    } else if (document.fullscreenElement) {
        document.exitFullscreen?.();
    }
}

export function setBlockDirection(editor, dir) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) {
        if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnClientError', 'no-selection', 'Select a block to change its direction.');
        return;
    }
    let node = sel.anchorNode;
    if (node && node.nodeType === 3) node = node.parentNode;
    let block = node;
    while (block && block !== editor && getComputedStyle(block).display === 'inline') block = block.parentNode;
    if (block && block !== editor) {
        block.setAttribute('dir', dir);
        afterChange(editor);
    }
}

// ---- toolbar roving tabindex (Req 20) ----
export function enableToolbarRoving(toolbar) {
    if (!toolbar || toolbar._roving) return;
    toolbar._roving = true;
    const items = () => [...toolbar.querySelectorAll('button,select,input,label')];
    const setTabs = (activeIdx) => {
        const list = items();
        list.forEach((el, i) => el.tabIndex = i === activeIdx ? 0 : -1);
    };
    setTabs(0);
    toolbar.addEventListener('keydown', (e) => {
        const list = items();
        let idx = list.indexOf(document.activeElement);
        if (idx < 0) return;
        if (e.key === 'ArrowRight') { e.preventDefault(); idx = (idx + 1) % list.length; }
        else if (e.key === 'ArrowLeft') { e.preventDefault(); idx = (idx - 1 + list.length) % list.length; }
        else if (e.key === 'Home') { e.preventDefault(); idx = 0; }
        else if (e.key === 'End') { e.preventDefault(); idx = list.length - 1; }
        else return;
        setTabs(idx);
        list[idx].focus();
    });
    toolbar.addEventListener('focusin', (e) => {
        const list = items();
        const idx = list.indexOf(e.target);
        if (idx >= 0) setTabs(idx);
    });
}

const IMAGE_MIME = ['image/png', 'image/jpeg', 'image/gif', 'image/webp', 'image/svg+xml'];
const MAX_IMAGE_BYTES = 10 * 1024 * 1024;

async function handleImageFiles(editor, files) {
    let accepted = 0;
    for (const file of files) {
        if (accepted >= 20) {
            reportClientError(editor, 'too-many-files', 'Only 20 images can be inserted per drop.');
            break;
        }
        if (!IMAGE_MIME.includes(file.type)) {
            reportClientError(editor, 'invalid-file', `"${file.name}" is not a supported image type.`);
            continue;
        }
        if (file.size > MAX_IMAGE_BYTES) {
            reportClientError(editor, 'file-too-large', `"${file.name}" exceeds the 10 MB limit.`);
            continue;
        }
        accepted++;
        const dataUrl = await readAsDataUrl(file);
        let url = dataUrl;
        if (editor._hasUpload && editor._dotNetRef) {
            const base64 = (dataUrl.split(',')[1]) ?? '';
            url = await editor._dotNetRef.invokeMethodAsync('ResolveImageUrl', file.name, file.type, base64);
            if (!url) continue;   // C# already surfaced the failure
        }
        dispatch(editor, 'insertImage', { html: `<img src="${escapeAttr(url)}" alt="${escapeAttr(file.name)}">` });
    }
    if (editor._notify) editor._notify();
}

function readAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const fr = new FileReader();
        fr.onload = () => resolve(fr.result);
        fr.onerror = () => reject(fr.error);
        fr.readAsDataURL(file);
    });
}

function reportClientError(editor, code, message) {
    if (editor._dotNetRef) editor._dotNetRef.invokeMethodAsync('OnClientError', code, message);
}

// ---- image resize (Req 7.9 / 7.10) ----
export function enableImageResize(editor) {
    if (!editor || editor._resizeWired) return;
    editor._resizeWired = true;
    editor.addEventListener('click', (e) => {
        if (e.target && e.target.tagName === 'IMG') startImageResize(editor, e.target);
        else removeResizeHandle(editor);
    });
}

function startImageResize(editor, img) {
    removeResizeHandle(editor);
    const handle = document.createElement('span');
    handle.className = 'blazor-rte-resize-handle';
    handle.contentEditable = 'false';
    Object.assign(handle.style, {
        position: 'absolute', width: '12px', height: '12px',
        background: '#0969da', border: '2px solid #fff', borderRadius: '2px',
        cursor: 'nwse-resize', zIndex: '5'
    });
    document.body.appendChild(handle);
    editor._resizeHandle = handle;

    const place = () => {
        const r = img.getBoundingClientRect();
        handle.style.left = `${window.scrollX + r.right - 6}px`;
        handle.style.top = `${window.scrollY + r.bottom - 6}px`;
    };
    place();
    editor._resizeReposition = place;
    window.addEventListener('scroll', place, true);

    handle.addEventListener('mousedown', (e) => {
        e.preventDefault();
        const startX = e.clientX;
        const startW = img.getBoundingClientRect().width;
        const maxW = editor.clientWidth;
        const onMove = (m) => {
            let w = Math.round(startW + (m.clientX - startX));
            w = Math.max(16, Math.min(w, maxW));
            img.style.width = `${w}px`;
            place();
        };
        const onUp = () => {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            const finalW = Math.max(16, Math.min(Math.round(img.getBoundingClientRect().width), editor.clientWidth));
            img.setAttribute('width', String(finalW));
            img.style.width = `${finalW}px`;
            if (editor._notify) editor._notify();
        };
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    });
}

function removeResizeHandle(editor) {
    if (editor._resizeHandle) {
        editor._resizeHandle.remove();
        editor._resizeHandle = null;
    }
    if (editor._resizeReposition) {
        window.removeEventListener('scroll', editor._resizeReposition, true);
        editor._resizeReposition = null;
    }
}

// ---------------------------------------------------------------------------
// Events
// ---------------------------------------------------------------------------
function onPaste(editor, e) {
    const cb = e.clipboardData;
    if (!cb) return;

    // Image paste (Req 7.5): if the clipboard carries image data, handle it and stop.
    const imageFiles = [...(cb.items || [])]
        .filter(it => it.kind === 'file' && it.type.startsWith('image/'))
        .map(it => it.getAsFile())
        .filter(Boolean);
    if (imageFiles.length > 0) {
        e.preventDefault();
        handleImageFiles(editor, imageFiles);
        return;
    }

    e.preventDefault();
    const html = cb.getData('text/html');
    const text = cb.getData('text/plain');
    const plainOnly = editor._plainTextPaste === true;
    let toInsert = (!plainOnly && html)
        ? sanitize(editor, normalizeWordHtml(html))
        : escapeHtml(text).replace(/\r?\n/g, '<br>');

    // MaxLength: truncate paste so the limit is not exceeded (Req 19.6).
    const max = editor._maxLength;
    if (max != null) {
        const current = (editor.textContent || '').length;
        const remaining = Math.max(0, max - current);
        if (remaining === 0) return;
        if (text.length > remaining) {
            toInsert = escapeHtml(text.slice(0, remaining)).replace(/\r?\n/g, '<br>');
        }
    }
    dispatch(editor, 'insertHtml', { html: toInsert });
    if (editor._notify) editor._notify();
}

function onDrop(editor, e) {
    const dt = e.dataTransfer;
    if (!dt) return;
    const imageFiles = [...(dt.files || [])].filter(f => f.type.startsWith('image/'));
    if (imageFiles.length === 0) return;   // let the browser handle non-image drops
    e.preventDefault();
    // Move the caret to the drop point when the browser supports it.
    const range = caretRangeFromPoint(e.clientX, e.clientY);
    if (range) {
        const sel = document.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
        editor._range = range.cloneRange();
    }
    handleImageFiles(editor, [...dt.files]);
}

function caretRangeFromPoint(x, y) {
    if (document.caretRangeFromPoint) return document.caretRangeFromPoint(x, y);
    if (document.caretPositionFromPoint) {
        const p = document.caretPositionFromPoint(x, y);
        if (p) { const r = document.createRange(); r.setStart(p.offsetNode, p.offset); r.collapse(true); return r; }
    }
    return null;
}

function onKeyDown(editor, e) {
    if (!(e.ctrlKey || e.metaKey)) return;
    const key = e.key.toLowerCase();
    // Normalize the primary modifier (Ctrl on Windows/Linux, Cmd on macOS) to "ctrl".
    const primary = e.ctrlKey || e.metaKey;
    if (editor._dotNetRef) {
        editor._dotNetRef.invokeMethodAsync('OnShortcut', key, primary, e.shiftKey, e.altKey);
    }
    // Prevent the browser default for known formatting combos so C# stays in control.
    if (['b', 'i', 'u'].includes(key)) e.preventDefault();
}

function onBeforeInput(editor, e) {
    const max = editor._maxLength;
    if (max == null) return;
    const current = (editor.textContent || '').length;

    // Insertions that add characters.
    const isInsert = e.inputType && e.inputType.startsWith('insert');
    if (!isInsert) return;

    if (e.inputType === 'insertFromPaste') {
        // Allow paste handler to truncate; nothing to do here.
        return;
    }
    const adding = (e.data ? e.data.length : 1);
    if (current + adding > max) {
        e.preventDefault();
    }
}

// ---------------------------------------------------------------------------
// Selection state + content facts
// ---------------------------------------------------------------------------
function afterChange(editor) {
    if (!editor._dotNetRef) return;
    editor._dotNetRef.invokeMethodAsync('OnContentChanged', editor.innerHTML, computeFacts(editor));
    reportState(editor);
}

function reportState(editor) {
    if (!editor._dotNetRef) return;
    editor._dotNetRef.invokeMethodAsync('OnSelectionChanged', currentState(editor));
}

function currentState(editor) {
    const q = (c) => { try { return document.queryCommandState(c); } catch { return false; } };
    const v = (c) => { try { return (document.queryCommandValue(c) || '').toString(); } catch { return ''; } };
    let block = '';
    try { block = (document.queryCommandValue('formatBlock') || '').toString().toLowerCase(); } catch { /* ignore */ }

    const link = linkAtSelection(editor);
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
        block: block,
        subscript: q('subscript'),
        superscript: q('superscript'),
        foreColor: v('foreColor') || null,
        backColor: v('backColor') || null,
        fontName: (v('fontName') || '').replace(/^['"]|['"]$/g, '') || null,
        fontSize: v('fontSize') || null,
        direction: directionAtSelection(editor),
        inLink: !!link,
        linkHref: link ? link.getAttribute('href') : null
    };
}

function computeFacts(editor) {
    const text = (editor.textContent || '').replace(/\u00a0/g, ' ');
    const hasText = text.trim().length > 0;
    const hasEmbedded = !!editor.querySelector('img,table,hr,audio,video,iframe');
    const chars = text.replace(/\s+$/g, '').length === 0 && !hasText ? 0 : text.length;
    const words = (text.trim().match(/\S+/g) || []).length;
    return {
        hasText: hasText,
        hasEmbeddedContent: hasEmbedded,
        characterCount: hasText ? text.length : (chars),
        wordCount: words
    };
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function linkAtSelection(editor) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    let node = sel.anchorNode;
    while (node && node !== editor) {
        if (node.nodeType === 1 && node.tagName === 'A') return node;
        node = node.parentNode;
    }
    return null;
}

function directionAtSelection(editor) {
    const sel = document.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    let node = sel.anchorNode;
    if (node && node.nodeType === 3) node = node.parentNode;
    while (node && node !== editor) {
        if (node.nodeType === 1 && node.dir) return node.dir;
        node = node.parentNode;
    }
    return null;
}

function insertNodeHtml(editor, html) {
    if (!html) return false;
    return execNative(editor, 'insertHTML', html);
}

function insertHorizontalRule(editor) {
    if (!execNative(editor, 'insertHorizontalRule')) {
        return execNative(editor, 'insertHTML', '<hr>');
    }
    return true;
}

function createLinkImpl(editor, url) {
    if (!url) return false;
    const sel = document.getSelection();
    if (sel && sel.isCollapsed) {
        return execNative(editor, 'insertHTML',
            `<a href="${escapeAttr(url)}">${escapeHtml(url)}</a>`);
    }
    return execNative(editor, 'createLink', url);
}

function restoreSelection(editor) {
    const r = editor._range;
    if (!r) return;
    const sel = document.getSelection();
    if (!sel) return;
    sel.removeAllRanges();
    sel.addRange(r);
}

// Allowlist-aware sanitize. When a policy is present it is applied; otherwise a secure
// default (strip script/style/embeds + event handlers + javascript: URIs) is used.
function sanitize(editor, html) {
    const tpl = document.createElement('template');
    tpl.innerHTML = html;
    const policy = editor && editor._policy;

    tpl.content.querySelectorAll('script,style,iframe,object,embed,link,meta,title,head').forEach(n => {
        // iframe may be allowed by an explicit policy (media embeds).
        if (policy && policy.allowedTags && policy.allowedTags.includes(n.tagName.toLowerCase())) return;
        n.remove();
    });

    tpl.content.querySelectorAll('*').forEach(el => {
        const tag = el.tagName.toLowerCase();
        if (policy && policy.allowedTags && !policy.allowedTags.includes(tag)) {
            // Unwrap disallowed element, keep its children/text.
            el.replaceWith(...el.childNodes);
            return;
        }
        for (const attr of [...el.attributes]) {
            const name = attr.name.toLowerCase();
            const val = attr.value;
            if (name.startsWith('on')) { el.removeAttribute(attr.name); continue; }
            if ((name === 'href' || name === 'src') && /^\s*javascript:/i.test(val)) {
                el.removeAttribute(attr.name); continue;
            }
            if (policy && policy.allowedAttributes) {
                const allowed = policy.allowedAttributes[tag] || policy.allowedAttributes['*'] || [];
                if (!allowed.includes(name)) el.removeAttribute(attr.name);
            }
        }
    });
    return tpl.innerHTML;
}

// Strip Word/Google-Docs cruft before sanitization.
function normalizeWordHtml(html) {
    return html
        .replace(/<!--[\s\S]*?-->/g, '')      // conditional comments
        .replace(/<\/?o:[^>]*>/gi, '')          // <o:p> etc.
        .replace(/<\/?w:[^>]*>/gi, '')
        .replace(/\s(class|style)="[^"]*mso[^"]*"/gi, '');
}

function escapeHtml(s) {
    const d = document.createElement('div');
    d.textContent = s ?? '';
    return d.innerHTML;
}

function escapeAttr(s) {
    return (s ?? '').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
