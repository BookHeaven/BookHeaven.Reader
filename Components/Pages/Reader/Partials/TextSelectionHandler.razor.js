const Reference = {
    DotNet: null,
};
const highlightedRanges = [];
let selectionChangeTimeout;

export function Initialize(dotnet) {
    Reference.DotNet = dotnet;
    window.addEventListener('resize', clampSelectionAction);
    document.addEventListener('selectionchange', debounceSelectionChange);
    refreshHighlights();
}

export function Dispose() {
    window.removeEventListener('resize', clampSelectionAction);
    document.removeEventListener('selectionchange', debounceSelectionChange);
    clearTimeout(selectionChangeTimeout);
    highlightedRanges.length = 0;
    if (window.CSS?.highlights) CSS.highlights.delete('bookheaven-highlight');
}

export function ClampSelectionAction() {
    const action = document.getElementById('reader-selection-action');
    if (!action) return;

    const bounds = action.getBoundingClientRect();
    const margin = 8;
    const anchorX = Number(action.dataset.anchorX);
    const desiredLeft = anchorX - bounds.width / 2;
    const maxLeft = Math.max(margin, window.innerWidth - bounds.width - margin);
    const maxTop = Math.max(margin, window.innerHeight - bounds.height - margin);
    action.style.left = `${Math.min(Math.max(desiredLeft, margin), maxLeft)}px`;
    action.style.top = `${Math.min(Math.max(bounds.top, margin), maxTop)}px`;
}

const clampSelectionAction = () => ClampSelectionAction();

const refreshHighlights = () => {
    if (!window.CSS?.highlights || typeof Highlight === 'undefined') return;
    CSS.highlights.set('bookheaven-highlight', new Highlight(...highlightedRanges));
};

const getTextNodes = (root) => {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    return nodes;
};

const getTextOffset = (root, container, offset) => {
    const range = document.createRange();
    range.selectNodeContents(root);
    range.setEnd(container, offset);
    return range.toString().length;
};

const createRangeFromOffsets = (root, start, end) => {
    const range = document.createRange();
    let position = 0;
    let startSet = false;
    for (const node of getTextNodes(root)) {
        const length = node.textContent.length;
        if (!startSet && start <= position + length) {
            range.setStart(node, start - position);
            startSet = true;
        }
        if (startSet && end <= position + length) {
            range.setEnd(node, end - position);
            return range;
        }
        position += length;
    }
    return null;
};

export function RestoreHighlights(highlights) {
    highlightedRanges.length = 0;
    const chapter = document.querySelector('.chapter.current');
    if (chapter) {
        for (const highlight of highlights) {
            const range = createRangeFromOffsets(chapter, highlight.startOffset, highlight.endOffset);
            if (range) highlightedRanges.push(range);
        }
    }
    refreshHighlights();
}

export function GetPageForTextOffset(offset) {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return null;

    const textLength = getTextNodes(chapter).reduce((total, node) => total + node.textContent.length, 0);
    if (offset < 0 || offset >= textLength) return null;

    const range = createRangeFromOffsets(chapter, offset, offset + 1);
    const rect = range?.getClientRects()[0];
    if (!rect) return null;

    const style = getComputedStyle(chapter);
    const columnWidth = parseFloat(style.columnWidth);
    const columnGap = parseFloat(style.columnGap);
    const columnCount = Number.parseInt(style.getPropertyValue('--column-count'), 10) || 1;
    if (!Number.isFinite(columnWidth) || !Number.isFinite(columnGap) || columnWidth <= 0) return null;

    const columnIndex = Math.max(0, Math.floor((rect.left - chapter.getBoundingClientRect().left) / (columnWidth + columnGap)));
    return Math.floor(columnIndex / columnCount) + 1;
}

export const SelectHighlightAt = (x, y) => {
    const chapter = document.querySelector('.chapter.current');
    const offset = GetTextOffsetAt(x, y);
    if (!chapter || offset === null) return false;

    for (const range of highlightedRanges) {
        const start = getTextOffset(chapter, range.startContainer, range.startOffset);
        const end = getTextOffset(chapter, range.endContainer, range.endOffset);
        if (offset < start || offset >= end) continue;

        const highlightRange = createRangeFromOffsets(chapter, start, end);
        if (!highlightRange) return false;
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(highlightRange);
        return true;
    }
    return false;
};

export const GetTextOffsetAt = (x, y) => {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return null;

    let container, offset;
    if (document.caretRangeFromPoint) {
        const range = document.caretRangeFromPoint(x, y);
        if (!range) return null;
        container = range.startContainer;
        offset = range.startOffset;
    } else if (document.caretPositionFromPoint) {
        const position = document.caretPositionFromPoint(x, y);
        if (!position) return null;
        container = position.offsetNode;
        offset = position.offset;
    } else {
        return null;
    }

    if (!chapter.contains(container)) return null;
    return getTextOffset(chapter, container, offset);
};

const getSelectionOffsets = (selection, chapter) => {
    const range = selection.getRangeAt(0);
    return {
        startOffset: getTextOffset(chapter, range.startContainer, range.startOffset),
        endOffset: getTextOffset(chapter, range.endContainer, range.endOffset),
        text: range.toString()
    };
};

export function ConsumeSelection() {
    const selection = window.getSelection();
    const chapter = document.querySelector('.chapter.current');
    if (!selection || selection.isCollapsed || selection.rangeCount === 0 || !chapter) return null;
    if (!chapter.contains(selection.getRangeAt(0).commonAncestorContainer)) return null;

    const offsets = getSelectionOffsets(selection, chapter);
    selection.removeAllRanges();
    return offsets;
}

const notifyNoSelection = () => Reference.DotNet.invokeMethodAsync('OnTextSelected', null, 0, 0, 0, 0);

const debounceSelectionChange = () => {
    clearTimeout(selectionChangeTimeout);
    selectionChangeTimeout = setTimeout(() => {
        if (document.querySelector('.reader-overlay')) {
            notifyNoSelection();
            return;
        }

        const selection = window.getSelection();
        const text = selection?.toString().trim();
        const chapter = document.querySelector('.chapter.current');
        if (!text || !selection || selection.rangeCount === 0 || !chapter) {
            notifyNoSelection();
            return;
        }

        const range = selection.getRangeAt(0);
        if (!chapter.contains(range.commonAncestorContainer)) {
            notifyNoSelection();
            return;
        }

        const bounds = range.getBoundingClientRect();
        const left = bounds.left + bounds.width / 2;
        const top = bounds.top > 56 ? bounds.top - 48 : bounds.bottom + 8;
        const { startOffset, endOffset } = getSelectionOffsets(selection, chapter);
        Reference.DotNet.invokeMethodAsync('OnTextSelected', text, left, top, startOffset, endOffset);
    }, 100);
};
