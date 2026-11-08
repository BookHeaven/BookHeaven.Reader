const Reference = {
    DotNet: null,
};
const highlightedRanges = [];
const highlightedOffsets = [];
const selectionPageTextRangeCache = new Map();
const selectionPageTurnDelay = 1200;
const selectionChangeDelay = 100;
const longPressDelay = 500;
const selectionMoveThreshold = 4;
const selectionHandleKnobCenterOffsetY = 25;
const selectionHandleHeight = 42;
const selectionHandleMouseHitboxHeight = 16;
const selectionHandleTouchStemExtension = 6;

let activeSelection = null;
let selectionInputType = 'mouse';
let selectionChapter = null;
let selectionChapterFirstChild = null;
let editingHighlightRange = null;
let editingHighlightChanged = false;
let selectionAcrossPages = false;
let selectionPointer = null;
let selectionPageTurnTimeout;
let selectionPageTurnDirection = 0;
let selectionPageTurnBlockedDirection = 0;
let selectionPageTurnInProgress = false;
let selectionChangeTimeout;
let selectionCancellationTimeout;
let selectionCancellationPending = false;
let pageStyleObserver;
let observedPageStyle;
let observedPageMarker;
let chapterMutationObserver;
let selectionOverlay;
let selectionStartHandle;
let selectionEndHandle;
let fallbackSelectionRects = [];

export function Initialize(dotnet, startHandleLabel, endHandleLabel) {
    Reference.DotNet = dotnet;
    createSelectionOverlay(startHandleLabel, endHandleLabel);
    window.getSelection()?.removeAllRanges();
    updateSelectionEventListeners(true);
    refreshHighlights();
    observeReaderLayout();
}

export function Dispose() {
    updateSelectionEventListeners(false);
    clearTimeout(selectionChangeTimeout);
    clearTimeout(selectionCancellationTimeout);
    clearSelectionPageTurnTimer();
    clearTimeout(selectionPointer?.longPressTimeout);
    pageStyleObserver?.disconnect();
    chapterMutationObserver?.disconnect();
    activeSelection = null;
    selectionChapter = null;
    selectionChapterFirstChild = null;
    clearEditingHighlightRange();
    selectionPointer = null;
    selectionCancellationPending = false;
    selectionAcrossPages = false;
    selectionPageTurnBlockedDirection = 0;
    selectionPageTurnInProgress = false;
    selectionOverlay?.remove();
    selectionOverlay = null;
    selectionStartHandle = null;
    selectionEndHandle = null;
    clearFallbackSelectionRects();
    if (window.CSS?.highlights) {
        CSS.highlights.delete('bookheaven-highlight');
        CSS.highlights.delete('bookheaven-selection');
    }
}

const selectionEventListeners = () => [
    [window, 'resize', handleReaderLayoutChange],
    [document, 'pointerdown', handleSelectionPointerDown, true],
    [document, 'pointermove', handleSelectionPointerMove, true],
    [document, 'pointerup', handleSelectionPointerUp, true],
    [document, 'pointercancel', handleSelectionPointerUp, true],
    [document, 'contextmenu', handleSelectionContextMenu, true]
];

function updateSelectionEventListeners(add) {
    for (const [target, type, listener, capture = false] of selectionEventListeners()) {
        target[add ? 'addEventListener' : 'removeEventListener'](type, listener, capture);
    }
}

const createSelectionOverlay = (startHandleLabel, endHandleLabel) => {
    selectionOverlay = document.createElement('div');
    Object.assign(selectionOverlay.style, {
        position: 'fixed',
        inset: '0',
        zIndex: '999',
        pointerEvents: 'none'
    });

    selectionStartHandle = createSelectionHandle(startHandleLabel);
    selectionEndHandle = createSelectionHandle(endHandleLabel);
    selectionOverlay.append(selectionStartHandle, selectionEndHandle);
    document.body.append(selectionOverlay);
};

const createSelectionHandle = label => {
    const handle = document.createElement('button');
    handle.type = 'button';
    handle.className = 'reader-selection-handle';
    handle.setAttribute('aria-label', label);
    Object.assign(handle.style, {
        position: 'fixed',
        display: 'none',
        width: '36px',
        height: `${selectionHandleMouseHitboxHeight}px`,
        padding: '0',
        border: '0',
        background: 'transparent',
        pointerEvents: 'auto',
        touchAction: 'none',
        cursor: 'grab',
        zIndex: '1'
    });

    const stem = document.createElement('span');
    Object.assign(stem.style, {
        position: 'absolute',
        left: '17px',
        top: '-10px',
        width: '2px',
        height: '10px',
        background: '#1677ff',
        pointerEvents: 'none'
    });
    const knob = document.createElement('span');
    Object.assign(knob.style, {
        position: 'absolute',
        left: '10px',
        top: '0',
        width: '16px',
        height: '16px',
        border: '2px solid white',
        borderRadius: '50%',
        boxSizing: 'border-box',
        background: '#1677ff',
        boxShadow: '0 1px 3px rgba(0, 0, 0, .45)',
        pointerEvents: 'none'
    });
    handle.append(stem, knob);
    handle.addEventListener('keydown', event => moveSelectionHandleWithKeyboard(event, handle));
    return handle;
};

const observeReaderLayout = () => {
    pageStyleObserver?.disconnect();
    observedPageStyle = Array.from(document.querySelectorAll('style'))
        .find(style => style.textContent.includes('--chapter-page')) ?? null;
    observedPageMarker = getPageMarker();
    if (observedPageStyle) {
        pageStyleObserver = new MutationObserver(handleReaderLayoutChange);
        pageStyleObserver.observe(observedPageStyle, {
            childList: true,
            characterData: true,
            subtree: true
        });
    }

    observeCurrentChapter();
};

const observeCurrentChapter = () => {
    chapterMutationObserver?.disconnect();
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return;

    chapterMutationObserver = new MutationObserver(() => {
        selectionPageTextRangeCache.clear();
        if (selectionChapter && selectionChapterFirstChild !== selectionChapter.firstChild) {
            clearSelectionState();
            notifyNoSelection();
            return;
        }
        renderSelection();
    });
    chapterMutationObserver.observe(chapter, { childList: true });
};

const getPageMarker = () => {
    const chapter = document.querySelector('.chapter.current');
    return chapter
        ? getComputedStyle(chapter).getPropertyValue('--chapter-page').trim()
        : null;
};

const handleReaderLayoutChange = () => {
    selectionPageTextRangeCache.clear();
    const pageMarker = getPageMarker();
    const pageChanged = pageMarker !== observedPageMarker;
    observedPageMarker = pageMarker;
    observeCurrentChapter();

    requestAnimationFrame(() => {
        if (!isSelectionRootCurrent()) return;
        renderSelection();
        if (pageChanged && HasTextSelection()) debounceSelectionChange();
        ClampSelectionAction();
    });
};

export function ClampSelectionAction() {
    const action = document.getElementById('reader-selection-action');
    if (!action) return;

    const actionBounds = action.getBoundingClientRect();
    const margin = 24;
    let anchorX = Number(action.dataset.anchorX);
    let desiredTop = actionBounds.top;
    const selection = getCurrentSelectionRange();
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    const viewerBounds = viewer?.getBoundingClientRect();

    if (selection && viewer && viewerBounds) {
        const visibleRects = getVisibleSelectionRects(selection, viewerBounds);
        if (visibleRects.length > 0) {
            const selectedBounds = visibleRects.reduce((bounds, rect) => ({
                left: Math.min(bounds.left, rect.left),
                right: Math.max(bounds.right, rect.right),
                top: Math.min(bounds.top, rect.top),
                bottom: Math.max(bounds.bottom, rect.bottom)
            }), {
                left: Number.POSITIVE_INFINITY,
                right: Number.NEGATIVE_INFINITY,
                top: Number.POSITIVE_INFINITY,
                bottom: Number.NEGATIVE_INFINITY
            });
            const focusAtStart = activeSelection.focusOffset < activeSelection.anchorOffset;
            const focusPoint = getVisibleCaretPoint(
                chapter,
                activeSelection.focusOffset,
                viewerBounds,
                focusAtStart);
            if (focusPoint) anchorX = focusPoint.x;
            const spaceAbove = selectedBounds.top - viewerBounds.top;
            const spaceBelow = viewerBounds.bottom - selectedBounds.bottom - selectionHandleHeight;
            if (spaceAbove >= actionBounds.height + margin) {
                desiredTop = selectedBounds.top - actionBounds.height - margin;
            } else if (spaceBelow >= actionBounds.height + margin) {
                desiredTop = selectedBounds.bottom + selectionHandleHeight + margin;
            } else {
                desiredTop = spaceAbove > spaceBelow
                    ? viewerBounds.top + margin
                    : viewerBounds.bottom - actionBounds.height - margin;
            }
        }
    }

    const desiredLeft = anchorX - actionBounds.width / 2;
    let minLeft = margin;
    let maxLeft = Math.max(margin, window.innerWidth - actionBounds.width - margin);
    let minTop = margin;
    let maxTop = Math.max(margin, window.innerHeight - actionBounds.height - margin);
    if (viewerBounds && viewerBounds.width >= actionBounds.width + margin * 2) {
        minLeft = Math.max(minLeft, viewerBounds.left + margin);
        maxLeft = Math.min(maxLeft, viewerBounds.right - actionBounds.width - margin);
    }
    if (viewerBounds && viewerBounds.height >= actionBounds.height + margin * 2) {
        minTop = Math.max(minTop, viewerBounds.top + margin);
        maxTop = Math.min(maxTop, viewerBounds.bottom - actionBounds.height - margin);
    }
    action.style.left = `${Math.min(Math.max(desiredLeft, minLeft), maxLeft)}px`;
    action.style.top = `${Math.min(Math.max(desiredTop, minTop), maxTop)}px`;
}

const handleSelectionPointerDown = event => {
    clearTimeout(selectionCancellationTimeout);
    selectionCancellationPending = false;
    if (event.button !== 0) return;

    const handleAction = event.target.closest?.('#reader-selection-action');
    const handle = event.target.closest?.('.reader-selection-handle') ??
        (event.pointerType === 'touch' && !handleAction
            ? getTouchSelectionHandleAtPoint(event.clientX, event.clientY)
            : null);
    if (handle) {
        setSelectionInputType(event.pointerType);
        beginSelectionHandleDrag(event, handle);
        return;
    }
    if (handleAction) return;
    if (event.target.closest?.('.reader-surface')) setSelectionInputType(event.pointerType);

    if (HasTextSelection() &&
        !event.target.closest?.('#reader-selection-action') &&
        !isPointWithinSelection(event.clientX, event.clientY)) {
        clearSelectionState();
        notifyNoSelection();
        selectionCancellationPending = true;
        selectionCancellationTimeout = setTimeout(() => {
            selectionCancellationPending = false;
        }, 1000);
        return;
    }

    if (!event.target.closest?.('.reader-surface')) return;

    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!viewer || !event.target.closest('.viewer')) {
        clearSelectionState();
        notifyNoSelection();
        return;
    }

    const pointerCaret = chapter && viewer
        ? getPointerCaret(event.clientX, event.clientY, chapter, viewer)
        : null;

    if (!pointerCaret?.isOverText) {
        clearSelectionState();
        notifyNoSelection();
        return;
    }

    const isTouchingHighlight =
        getHighlightRangeAtPoint(event.clientX, event.clientY) !== undefined;
    if (!isTouchingHighlight) clearEditingHighlightRange();
    notifyNoSelection();
    clearTimeout(selectionPointer?.longPressTimeout);
    selectionPointer = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
        originX: event.clientX,
        originY: event.clientY,
        anchorOffset: pointerCaret?.position
            ? getTextOffset(chapter, pointerCaret.position.container, pointerCaret.position.offset)
            : null,
        moved: false,
        mode: isTouchingHighlight ? 'highlight-pending' : 'pending',
        longPressTimeout: null
    };

    if (selectionPointer.anchorOffset !== null) {
        selectionPointer.longPressTimeout = setTimeout(
            () => selectLongPressAtPointer(selectionPointer.id),
            longPressDelay);
    }
    selectionPageTurnBlockedDirection = 0;
};

const selectLongPressAtPointer = pointerId => {
    if (selectionPointer?.id !== pointerId || selectionPointer.moved) return;

    if (selectionPointer.mode === 'highlight-pending' &&
        SelectHighlightAt(selectionPointer.x, selectionPointer.y)) {
        selectionPointer.mode = 'highlight';
        selectionPointer.moved = true;
        return;
    }

    selectWordAtPointer(pointerId);
};

const selectWordAtPointer = pointerId => {
    if (selectionPointer?.id !== pointerId || selectionPointer.moved ||
        selectionPointer.anchorOffset === null) return;

    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return;
    const word = getWordOffsets(chapter, selectionPointer.anchorOffset);
    if (!word) return;

    clearEditingHighlightRange();
    const focusOffset = constrainSelectionOutsideHighlights(word.start, word.end);
    if (focusOffset === word.start) return;
    selectionPointer.mode = 'word';
    selectionPointer.anchorOffset = word.start;
    selectionPointer.wordStart = word.start;
    selectionPointer.wordEnd = focusOffset;
    selectionPointer.moved = false;
    setSelection(word.start, focusOffset, false);
    debounceSelectionChange();
};

const handleSelectionPointerMove = event => {
    if (event.pointerId !== selectionPointer?.id) return;

    selectionPointer.x = event.clientX;
    selectionPointer.y = event.clientY;
    if (selectionPointer.mode === 'highlight') return;

    if (selectionPointer.mode === 'pending' ||
        selectionPointer.mode === 'highlight-pending' ||
        selectionPointer.mode === 'word') {
        const wasHighlightPending = selectionPointer.mode === 'highlight-pending';
        const distance = Math.hypot(
            event.clientX - selectionPointer.originX,
            event.clientY - selectionPointer.originY);
        if (distance < selectionMoveThreshold) return;

        clearTimeout(selectionPointer.longPressTimeout);
        if (wasHighlightPending) {
            selectionPointer.mode = 'highlight-cancelled';
            selectionPointer.moved = true;
            return;
        }
        selectionPointer.mode = selectionPointer.mode === 'word' ? 'word-drag' : 'text';
        selectionPointer.moved = true;
    } else {
        selectionPointer.moved = true;
    }

    if (selectionPointer.mode === 'highlight-cancelled') return;

    if (selectionPointer.mode === 'word-drag') {
        updateWordSelectionFocusAtPointer();
    } else {
        updateSelectionFocusAtPointer();
    }
    scheduleSelectionPageTurn();
};

const updateSelectionFocusAtPointer = () => {
    if (!selectionPointer || selectionPointer.anchorOffset === null) return;
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!chapter || !viewer) return;

    const position = getPointerPosition(
        selectionPointer.mode === 'handle'
            ? selectionPointer.x - selectionPointer.caretOffsetX
            : selectionPointer.x,
        selectionPointer.mode === 'handle'
            ? selectionPointer.y - selectionPointer.caretOffsetY
            : selectionPointer.y,
        chapter,
        viewer);
    if (!position) return;

    const focusOffset = getTextOffset(chapter, position.container, position.offset);
    setSelection(
        selectionPointer.anchorOffset,
        constrainSelectionFocus(selectionPointer.anchorOffset, focusOffset),
        selectionAcrossPages);
    debounceSelectionChange();
};

const constrainEditedHighlightFocus = (anchorOffset, focusOffset) => {
    if (!editingHighlightRange) return focusOffset;

    const otherHighlights = highlightedOffsets.filter(highlight =>
        highlight.start !== editingHighlightRange.start ||
        highlight.end !== editingHighlightRange.end);
    if (focusOffset > anchorOffset) {
        const boundary = otherHighlights
            .filter(highlight => anchorOffset <= highlight.start && focusOffset > highlight.start)
            .reduce((nearest, highlight) => Math.min(nearest, highlight.start), Number.POSITIVE_INFINITY);
        if (Number.isFinite(boundary)) focusOffset = boundary;
    } else if (focusOffset < anchorOffset) {
        const boundary = otherHighlights
            .filter(highlight => anchorOffset >= highlight.end && focusOffset < highlight.end)
            .reduce((nearest, highlight) => Math.max(nearest, highlight.end), Number.NEGATIVE_INFINITY);
        if (Number.isFinite(boundary)) focusOffset = boundary;
    }

    if (anchorOffset <= editingHighlightRange.start ||
        anchorOffset >= editingHighlightRange.end) return focusOffset;

    if (focusOffset < anchorOffset) return Math.min(focusOffset, editingHighlightRange.start);
    if (focusOffset > anchorOffset) return Math.max(focusOffset, editingHighlightRange.end);
    return activeSelection.focusOffset;
};

const constrainSelectionOutsideHighlights = (anchorOffset, focusOffset) => {
    if (highlightedOffsets.some(highlight =>
        anchorOffset >= highlight.start && anchorOffset < highlight.end)) {
        return anchorOffset;
    }

    if (focusOffset > anchorOffset) {
        const boundary = highlightedOffsets
            .filter(highlight => anchorOffset <= highlight.start && focusOffset > highlight.start)
            .reduce((nearest, highlight) => Math.min(nearest, highlight.start), Number.POSITIVE_INFINITY);
        return Number.isFinite(boundary) ? boundary : focusOffset;
    }

    if (focusOffset < anchorOffset) {
        const boundary = highlightedOffsets
            .filter(highlight => anchorOffset >= highlight.end && focusOffset < highlight.end)
            .reduce((nearest, highlight) => Math.max(nearest, highlight.end), Number.NEGATIVE_INFINITY);
        return Number.isFinite(boundary) ? boundary : focusOffset;
    }

    return focusOffset;
};

const constrainSelectionFocus = (anchorOffset, focusOffset) =>
    editingHighlightRange
        ? constrainEditedHighlightFocus(anchorOffset, focusOffset)
        : constrainSelectionOutsideHighlights(anchorOffset, focusOffset);

const updateWordSelectionFocusAtPointer = () => {
    if (!selectionPointer || selectionPointer.anchorOffset === null) return;
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!chapter || !viewer) return;

    const position = getPointerPosition(
        selectionPointer.x,
        selectionPointer.y,
        chapter,
        viewer);
    if (!position) return;

    const pointerOffset = getTextOffset(chapter, position.container, position.offset);
    const word = getWordOffsets(chapter, pointerOffset);
    let focusOffset = selectionPointer.wordEnd;
    if (pointerOffset < selectionPointer.wordStart && word?.start < selectionPointer.wordStart) {
        focusOffset = word.start;
    } else if (pointerOffset >= selectionPointer.wordEnd && word?.end > selectionPointer.wordEnd) {
        focusOffset = word.end;
    }

    setSelection(
        selectionPointer.wordStart,
        constrainSelectionOutsideHighlights(selectionPointer.wordStart, focusOffset),
        selectionAcrossPages);
    debounceSelectionChange();
};

const handleSelectionPointerUp = event => {
    if (event.pointerId !== selectionPointer?.id) return;

    clearTimeout(selectionPointer.longPressTimeout);
    const selectionWasCreated = HasTextSelection();
    selectionPointer = null;
    selectionPageTurnBlockedDirection = 0;
    clearSelectionPageTurnTimer();
    if (selectionWasCreated) debounceSelectionChange();
};

const handleSelectionContextMenu = event => {
    if (!event.target.closest?.('.chapter.current')) return;
    event.preventDefault();
};

const getTouchSelectionHandleAtPoint = (x, y) => {
    if (!HasTextSelection() || !isSelectionRootCurrent()) return null;

    const { startOffset, endOffset } = getSelectionOffsets();
    const candidates = [
        { handle: selectionStartHandle, point: getTextCaretPoint(selectionChapter, startOffset, true) },
        { handle: selectionEndHandle, point: getTextCaretPoint(selectionChapter, endOffset, false) }
    ].filter(({ point }) =>
        point &&
        x >= point.x - 18 &&
        x <= point.x + 18 &&
        y >= point.y &&
        y <= point.y + selectionHandleHeight);

    candidates.sort((first, second) =>
        (x - first.point.x) ** 2 + (y - first.point.y - selectionHandleKnobCenterOffsetY) ** 2 -
        ((x - second.point.x) ** 2 + (y - second.point.y - selectionHandleKnobCenterOffsetY) ** 2));
    return candidates[0]?.handle ?? null;
};

const beginSelectionHandleDrag = (event, handle) => {
    event.preventDefault();
    event.stopPropagation();
    if (!isSelectionRootCurrent() || !HasTextSelection()) return;

    setSelectionInputType(event.pointerType);
    const { startOffset, endOffset } = getSelectionOffsets();
    const startPoint = getTextCaretPoint(selectionChapter, startOffset, true);
    const endPoint = getTextCaretPoint(selectionChapter, endOffset, false);
    const knobOffsetY = event.pointerType === 'touch' ? selectionHandleKnobCenterOffsetY : 0;
    const startDistance = startPoint
        ? (event.clientX - startPoint.x) ** 2 +
        (event.clientY - startPoint.y - knobOffsetY) ** 2
        : Number.POSITIVE_INFINITY;
    const endDistance = endPoint
        ? (event.clientX - endPoint.x) ** 2 +
        (event.clientY - endPoint.y - knobOffsetY) ** 2
        : Number.POSITIVE_INFINITY;
    const isStartHandle = startDistance === endDistance
        ? handle === selectionStartHandle
        : startDistance < endDistance;
    const handlePoint = isStartHandle ? startPoint : endPoint;
    const anchorOffset = isStartHandle ? endOffset : startOffset;
    const focusOffset = isStartHandle ? startOffset : endOffset;
    setSelection(
        anchorOffset,
        constrainSelectionFocus(anchorOffset, focusOffset),
        selectionAcrossPages);
    clearTimeout(selectionPointer?.longPressTimeout);
    selectionPointer = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
        originX: event.clientX,
        originY: event.clientY,
        anchorOffset,
        caretOffsetX: handlePoint ? event.clientX - handlePoint.x : 0,
        caretOffsetY: handlePoint ? event.clientY - handlePoint.y : 0,
        moved: false,
        mode: 'handle',
        longPressTimeout: null
    };
};

const moveSelectionHandleWithKeyboard = (event, handle) => {
    if ((event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') || !HasTextSelection()) return;
    event.preventDefault();

    setSelectionInputType('mouse');
    const { startOffset, endOffset } = getSelectionOffsets();
    const isStartHandle = handle === selectionStartHandle;
    const anchorOffset = isStartHandle ? endOffset : startOffset;
    const currentFocusOffset = isStartHandle ? startOffset : endOffset;
    const chapter = document.querySelector('.chapter.current');
    const isRightToLeft = getComputedStyle(chapter).direction === 'rtl';
    const delta = (event.key === 'ArrowRight' ? 1 : -1) * (isRightToLeft ? -1 : 1);
    const focusOffset = Math.min(
        getTextLength(chapter),
        Math.max(0, currentFocusOffset + delta));

    setSelection(
        anchorOffset,
        constrainEditedHighlightFocus(anchorOffset, focusOffset),
        selectionAcrossPages);
    debounceSelectionChange();
};

const clearSelectionPageTurnTimer = () => {
    clearTimeout(selectionPageTurnTimeout);
    selectionPageTurnTimeout = null;
    selectionPageTurnDirection = 0;
};

const getTextNodes = root => {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    return nodes;
};

const getTextLength = root =>
    getTextNodes(root).reduce((total, node) => total + node.textContent.length, 0);

const getPositionAtTextOffset = (root, offset) => {
    if (!root || offset < 0) return null;

    let position = 0;
    let lastNode = null;
    for (const node of getTextNodes(root)) {
        lastNode = node;
        const length = node.textContent.length;
        if (offset <= position + length) {
            return { container: node, offset: offset - position };
        }
        position += length;
    }

    return offset === position && lastNode
        ? { container: lastNode, offset: lastNode.textContent.length }
        : null;
};

const getTextOffset = (root, container, offset) => {
    if (!root?.contains(container)) return null;
    const range = document.createRange();
    range.selectNodeContents(root);
    range.setEnd(container, offset);
    return range.toString().length;
};

const createRangeFromOffsets = (root, start, end) => {
    const startPosition = getPositionAtTextOffset(root, Math.min(start, end));
    const endPosition = getPositionAtTextOffset(root, Math.max(start, end));
    if (!startPosition || !endPosition) return null;

    const range = document.createRange();
    range.setStart(startPosition.container, startPosition.offset);
    range.setEnd(endPosition.container, endPosition.offset);
    return range;
};

const getSelectionOffsets = () => ({
    startOffset: Math.min(activeSelection.anchorOffset, activeSelection.focusOffset),
    endOffset: Math.max(activeSelection.anchorOffset, activeSelection.focusOffset)
});

const getCurrentSelectionRange = () => {
    if (!HasTextSelection() || !isSelectionRootCurrent()) return null;
    const { startOffset, endOffset } = getSelectionOffsets();
    return createRangeFromOffsets(selectionChapter, startOffset, endOffset);
};

const isSelectionRootCurrent = () => {
    if (!activeSelection) return false;
    const chapter = document.querySelector('.chapter.current');
    if (chapter === selectionChapter && chapter?.firstChild === selectionChapterFirstChild) return true;

    clearSelectionState();
    notifyNoSelection();
    return false;
};

const setSelection = (anchorOffset, focusOffset, acrossPages) => {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter || !getPositionAtTextOffset(chapter, anchorOffset) ||
        !getPositionAtTextOffset(chapter, focusOffset)) return false;

    if (editingHighlightRange) {
        const startOffset = Math.min(anchorOffset, focusOffset);
        const endOffset = Math.max(anchorOffset, focusOffset);
        editingHighlightChanged =
            startOffset !== editingHighlightRange.start ||
            endOffset !== editingHighlightRange.end;
    }
    activeSelection = { anchorOffset, focusOffset };
    selectionChapter = chapter;
    selectionChapterFirstChild = chapter.firstChild;
    selectionAcrossPages = acrossPages;
    renderSelection();
    return true;
};

const clearSelectionState = () => {
    activeSelection = null;
    selectionChapter = null;
    selectionChapterFirstChild = null;
    clearEditingHighlightRange();
    selectionAcrossPages = false;
    clearTimeout(selectionPointer?.longPressTimeout);
    selectionPointer = null;
    clearTimeout(selectionChangeTimeout);
    clearSelectionPageTurnTimer();
    if (window.CSS?.highlights) CSS.highlights.delete('bookheaven-selection');
    hideSelectionHandles();
    clearFallbackSelectionRects();
};

const renderSelection = () => {
    const range = getCurrentSelectionRange();
    if (!range || range.collapsed) {
        if (window.CSS?.highlights) CSS.highlights.delete('bookheaven-selection');
        hideSelectionHandles();
        clearFallbackSelectionRects();
        return;
    }

    if (window.CSS?.highlights && typeof Highlight !== 'undefined') {
        CSS.highlights.set('bookheaven-selection', new Highlight(range));
        clearFallbackSelectionRects();
    } else {
        drawFallbackSelection(range);
    }
    positionSelectionHandles();
};

const drawFallbackSelection = range => {
    clearFallbackSelectionRects();
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    const bounds = viewer?.getBoundingClientRect();
    if (!bounds) return;

    for (const rect of getVisibleSelectionRects(range, bounds)) {
        const highlight = document.createElement('div');
        Object.assign(highlight.style, {
            position: 'fixed',
            left: `${rect.left}px`,
            top: `${rect.top}px`,
            width: `${rect.width}px`,
            height: `${rect.height}px`,
            background: 'rgba(51, 119, 255, .32)',
            pointerEvents: 'none'
        });
        selectionOverlay.insertBefore(highlight, selectionStartHandle);
        fallbackSelectionRects.push(highlight);
    }
};

const clearFallbackSelectionRects = () => {
    for (const rect of fallbackSelectionRects) rect.remove();
    fallbackSelectionRects = [];
};

const positionSelectionHandles = () => {
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    const bounds = viewer?.getBoundingClientRect();
    if (!chapter || !bounds) {
        hideSelectionHandles();
        return;
    }

    const { startOffset, endOffset } = getSelectionOffsets();
    let startPoint = getTextCaretPoint(chapter, startOffset, true);
    let endPoint = getTextCaretPoint(chapter, endOffset, false);
    const showStartHandle = startPoint !== null && isPointWithinBounds(startPoint, bounds);
    const showEndHandle = endPoint !== null && isPointWithinBounds(endPoint, bounds);
    selectionStartHandle.style.display = 'none';
    selectionEndHandle.style.display = 'none';

    if (showStartHandle) setHandlePosition(selectionStartHandle, startPoint);
    if (showEndHandle) setHandlePosition(selectionEndHandle, endPoint);
};

const isPointWithinBounds = (point, bounds) =>
    point.x >= bounds.left &&
    point.x <= bounds.right &&
    point.y >= bounds.top &&
    point.y <= bounds.bottom;

const setHandlePosition = (handle, point) => {
    const [stem, knob] = handle.children;
    const isTouch = selectionInputType === 'touch';
    const handleTop = point.y - selectionHandleMouseHitboxHeight / 2;
    const stemTop = point.y - point.lineHeight - handleTop;
    const stemExtension = isTouch ? selectionHandleTouchStemExtension : 0;
    stem.style.top = `${stemTop}px`;
    stem.style.height = `${point.lineHeight + stemExtension}px`;
    knob.style.top = `${stemTop + point.lineHeight + stemExtension}px`;
    handle.style.left = `${point.x - 18}px`;
    handle.style.top = `${handleTop}px`;
    handle.style.display = 'block';
};

const setSelectionInputType = pointerType => {
    const inputType = pointerType === 'touch' ? 'touch' : 'mouse';
    if (selectionInputType === inputType) return;

    selectionInputType = inputType;
    if (HasTextSelection()) renderSelection();
};

const hideSelectionHandles = () => {
    if (selectionStartHandle) selectionStartHandle.style.display = 'none';
    if (selectionEndHandle) selectionEndHandle.style.display = 'none';
};

const getTextCaretPoint = (root, offset, isStart) => {
    const position = getPositionAtTextOffset(root, offset);
    if (!position) return null;

    const caret = document.createRange();
    caret.setStart(position.container, position.offset);
    caret.collapse(true);
    const caretRect = caret.getBoundingClientRect();
    if (caretRect.height > 0) {
        return { x: caretRect.left, y: caretRect.bottom, lineHeight: caretRect.height };
    }

    const textLength = getTextLength(root);
    if (textLength === 0) return null;
    const useNextCharacter = offset < textLength;
    const characterRange = createRangeFromOffsets(
        root,
        useNextCharacter ? offset : offset - 1,
        useNextCharacter ? offset + 1 : offset);
    const rects = characterRange ? Array.from(characterRange.getClientRects()) : [];
    if (rects.length === 0) return null;

    const rect = useNextCharacter ? rects[0] : rects[rects.length - 1];
    const direction = getComputedStyle(position.container.parentElement ?? root).direction;
    const isRightToLeft = direction === 'rtl';
    const useLeftEdge = isStart !== isRightToLeft;
    return { x: useLeftEdge ? rect.left : rect.right, y: rect.bottom, lineHeight: rect.height };
};

const getVisibleCaretPoint = (root, offset, bounds, isStart) => {
    const point = getTextCaretPoint(root, offset, isStart);
    if (!point) return null;
    return {
        x: Math.min(bounds.right, Math.max(bounds.left, point.x)),
        y: Math.min(bounds.bottom, Math.max(bounds.top, point.y))
    };
};

const getVisibleSelectionRects = (range, bounds) =>
    Array.from(range.getClientRects())
        .filter(rect =>
            rect.width > 0 &&
            rect.height > 0 &&
            rect.left < bounds.right &&
            rect.right > bounds.left &&
            rect.top < bounds.bottom &&
            rect.bottom > bounds.top);

const getPointerCaret = (x, y, chapter, viewer) => {
    const position = getCaretPositionAtPoint(x, y, chapter);
    if (!position) return null;

    const range = document.createRange();
    range.setStart(position.container, position.offset);
    range.collapse(true);
    const rect = range.getBoundingClientRect();
    if (rect.height === 0) return null;

    return {
        position,
        isWithinViewer: isRectWithinViewer(rect, viewer.getBoundingClientRect()),
        isOverText: isPointNearText(x, y, chapter, position)
    };
};

const isPointNearText = (x, y, chapter, position) => {
    const offset = getTextOffset(chapter, position.container, position.offset);
    if (offset === null) return false;

    const textLength = getTextLength(chapter);
    const characterOffsets = [
        offset > 0 ? [offset - 1, offset] : null,
        offset < textLength ? [offset, offset + 1] : null
    ].filter(rangeOffsets => rangeOffsets !== null);
    return characterOffsets.some(([startOffset, endOffset]) => {
        const range = createRangeFromOffsets(chapter, startOffset, endOffset);
        return range && Array.from(range.getClientRects()).some(rect =>
            rect.width > 0 &&
            rect.height > 0 &&
            x >= rect.left - 2 &&
            x <= rect.right + 2 &&
            y >= rect.top - 2 &&
            y <= rect.bottom + 2);
    });
};

const isPointWithinSelection = (x, y) => {
    const range = getCurrentSelectionRange();
    if (!range) return false;

    return Array.from(range.getClientRects()).some(rect =>
        x >= rect.left - 1 &&
        x <= rect.right + 1 &&
        y >= rect.top - 1 &&
        y <= rect.bottom + 1);
};

const getPointerPosition = (x, y, chapter, viewer) => {
    const pointerCaret = getPointerCaret(x, y, chapter, viewer);
    if (pointerCaret?.isWithinViewer) return pointerCaret.position;
    return getNearestVisibleCaretPosition(x, y, chapter, viewer);
};

const getCaretPositionAtPoint = (x, y, chapter) => {
    let container;
    let offset;
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

    return chapter.contains(container) ? { container, offset } : null;
};

const getNearestVisibleCaretPosition = (x, y, chapter, viewer) => {
    const bounds = viewer.getBoundingClientRect();
    let nearestPoint = null;
    let nearestDistance = Infinity;

    for (const node of getTextNodes(chapter)) {
        const range = document.createRange();
        range.selectNodeContents(node);
        for (const rect of range.getClientRects()) {
            if (rect.width <= 0 || rect.height <= 0 ||
                rect.left >= bounds.right || rect.right <= bounds.left ||
                rect.top >= bounds.bottom || rect.bottom <= bounds.top) continue;

            const pointX = Math.min(rect.right - 0.5, Math.max(rect.left + 0.5, x));
            const pointY = Math.min(rect.bottom - 0.5, Math.max(rect.top + 0.5, y));
            const distance = (pointX - x) ** 2 + (pointY - y) ** 2;
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearestPoint = { x: pointX, y: pointY };
        }
    }

    return nearestPoint ? getCaretPositionAtPoint(nearestPoint.x, nearestPoint.y, chapter) : null;
};

const isRectWithinViewer = (rect, bounds) =>
    rect.left >= bounds.left &&
    rect.top >= bounds.top &&
    rect.right <= bounds.right &&
    rect.bottom <= bounds.bottom;

const getWordOffsets = (chapter, offset) => {
    const text = getTextNodes(chapter).map(node => node.textContent).join('');
    if (text.length === 0) return null;

    let index = Math.min(offset, text.length - 1);
    if (!isWordCharacter(text[index]) && index > 0 && isWordCharacter(text[index - 1])) index--;
    if (!isWordCharacter(text[index])) {
        while (index < text.length && !isWordCharacter(text[index])) index++;
    }
    if (index >= text.length) return null;

    let start = index;
    let end = index + 1;
    while (start > 0 && isWordCharacter(text[start - 1])) start--;
    while (end < text.length && isWordCharacter(text[end])) end++;
    return { start, end };
};

const isWordCharacter = character => /[\p{L}\p{N}\p{M}_]/u.test(character);

const getSelectionPageTurnDirection = () => {
    if (!selectionPointer?.moved || document.querySelector('.reader-overlay') ||
        !HasTextSelection()) return 0;

    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!viewer) return 0;

    const bounds = viewer.getBoundingClientRect();
    if (selectionPointer.y < bounds.top || selectionPointer.y >= bounds.bottom) return 0;

    const isForward = activeSelection.focusOffset >= activeSelection.anchorOffset;
    const page = Number.parseInt(getPageMarker(), 10);
    if (!Number.isFinite(page)) return 0;
    const pageTextRange = getSelectionPageTextOffsetRange(chapter, page);
    if (!pageTextRange || !isFocusNearPageTextBoundary(
        chapter,
        bounds,
        pageTextRange,
        activeSelection.focusOffset,
        isForward)) return 0;

    const edgeZone = Math.min(40, bounds.width / 4);
    if (isForward && selectionPointer.x > bounds.right - edgeZone) return 1;
    if (!isForward && selectionPointer.x < bounds.left + edgeZone) return -1;
    return 0;
};

const getSelectionPageTextOffsetRange = (chapter, page) => {
    if (selectionPageTextRangeCache.has(page)) {
        return selectionPageTextRangeCache.get(page);
    }

    const textLength = getTextLength(chapter);
    const findFirstOffset = predicate => {
        let low = 0;
        let high = textLength;
        while (low < high) {
            const middle = Math.floor((low + high) / 2);
            const offsetPage = getPageForTextOffsetForSearch(chapter, middle);
            if (offsetPage !== null && predicate(offsetPage)) {
                high = middle;
            } else {
                low = middle + 1;
            }
        }
        return low;
    };

    const startOffset = findFirstOffset(offsetPage => offsetPage >= page);
    const endOffset = findFirstOffset(offsetPage => offsetPage > page);
    const pageTextRange = startOffset < endOffset &&
    startOffset < textLength &&
    getPageForTextOffsetForSearch(chapter, startOffset) === page
        ? { startOffset, endOffset }
        : null;
    selectionPageTextRangeCache.set(page, pageTextRange);
    return pageTextRange;
};

const getPageForTextOffsetForSearch = (chapter, offset) => {
    const page = GetPageForTextOffset(offset);
    if (page !== null) return page;

    const point = getTextCaretPoint(chapter, offset, true);
    const style = getComputedStyle(chapter);
    const columnWidth = Number.parseFloat(style.columnWidth);
    const columnGap = Number.parseFloat(style.columnGap);
    const columnCount = Number.parseInt(style.getPropertyValue('--column-count'), 10) || 1;
    if (!point || !Number.isFinite(columnWidth) || !Number.isFinite(columnGap) || columnWidth <= 0) {
        return null;
    }

    const columnIndex = Math.max(0, Math.floor(
        (point.x - chapter.getBoundingClientRect().left) / (columnWidth + columnGap)));
    return Math.floor(columnIndex / columnCount) + 1;
};

const isFocusNearPageTextBoundary = (chapter, bounds, pageTextRange, focusOffset, isForward) => {
    const { startOffset, endOffset } = pageTextRange;
    if (isForward) {
        if (focusOffset < startOffset || focusOffset > endOffset) return false;
        if (focusOffset === endOffset) return true;
    } else {
        if (focusOffset < startOffset || focusOffset > endOffset) return false;
        if (focusOffset === startOffset) return true;
    }

    const range = isForward
        ? createRangeFromOffsets(chapter, focusOffset, endOffset)
        : createRangeFromOffsets(chapter, startOffset, focusOffset);
    if (!range) return false;

    const visibleRects = getVisibleSelectionRects(range, bounds);
    const lineCount = new Set(visibleRects.map(rect => Math.round(rect.top))).size;
    if (lineCount === 0) {
        return isForward
            ? endOffset - focusOffset <= 16
            : focusOffset - startOffset <= 16;
    }
    return lineCount <= 2;
};

const scheduleSelectionPageTurn = () => {
    if (selectionPageTurnInProgress) return;

    const direction = getSelectionPageTurnDirection();
    if (direction === 0) {
        selectionPageTurnBlockedDirection = 0;
        clearSelectionPageTurnTimer();
        return;
    }
    if (selectionPageTurnBlockedDirection === direction) return;
    selectionPageTurnBlockedDirection = 0;
    if (selectionPageTurnTimeout && selectionPageTurnDirection === direction) return;

    clearSelectionPageTurnTimer();
    selectionPageTurnDirection = direction;
    selectionPageTurnTimeout = setTimeout(() => {
        selectionPageTurnTimeout = null;
        turnSelectionPage(direction);
    }, selectionPageTurnDelay);
};

const waitForSelectionPageRender = (chapter, page, firstChild) => new Promise(resolve => {
    let frameId;
    let timeoutId;
    let finished = false;
    const finish = () => {
        if (finished) return;

        finished = true;
        cancelAnimationFrame(frameId);
        clearTimeout(timeoutId);
        requestAnimationFrame(resolve);
    };
    const checkForRender = () => {
        const currentChapter = document.querySelector('.chapter.current');
        const currentPage = getPageMarker();
        if (currentChapter !== chapter ||
            currentChapter?.firstChild !== firstChild ||
            currentPage !== page) {
            finish();
            return;
        }
        frameId = requestAnimationFrame(checkForRender);
    };

    timeoutId = setTimeout(finish, 1000);
    frameId = requestAnimationFrame(checkForRender);
});

const turnSelectionPage = async direction => {
    if (selectionPageTurnInProgress) return;

    selectionPageTurnInProgress = true;
    const chapterBeforeTurn = document.querySelector('.chapter.current');
    const pageBeforeTurn = getPageMarker();
    const firstChildBeforeTurn = chapterBeforeTurn?.firstChild;
    let shouldContinue = true;
    try {
        const pageChanged = await Reference.DotNet.invokeMethodAsync('OnSelectionPageTurn', direction);
        selectionPageTurnBlockedDirection = direction;
        if (!pageChanged) {
            shouldContinue = false;
            return;
        }

        await waitForSelectionPageRender(chapterBeforeTurn, pageBeforeTurn, firstChildBeforeTurn);
        if (!isSelectionRootCurrent()) {
            shouldContinue = false;
            return;
        }

        selectionAcrossPages = true;
        if (selectionPointer.mode === 'word-drag') {
            updateWordSelectionFocusAtPointer();
        } else {
            updateSelectionFocusAtPointer();
        }
    } catch (error) {
        shouldContinue = false;
        console.error('Unable to change page while selecting text.', error);
    } finally {
        selectionPageTurnInProgress = false;
        selectionPageTurnDirection = 0;
        if (shouldContinue) scheduleSelectionPageTurn();
    }
};

const refreshHighlights = () => {
    if (!window.CSS?.highlights || typeof Highlight === 'undefined') return;
    CSS.highlights.set('bookheaven-highlight', new Highlight(...highlightedRanges));
};

export function RestoreHighlights(highlights) {
    highlightedRanges.length = 0;
    highlightedOffsets.length = 0;
    const chapter = document.querySelector('.chapter.current');
    if (chapter) {
        for (const highlight of highlights) {
            const range = createRangeFromOffsets(chapter, highlight.startOffset, highlight.endOffset);
            if (range) {
                highlightedRanges.push(range);
                highlightedOffsets.push({ start: highlight.startOffset, end: highlight.endOffset });
            }
        }
    }
    refreshHighlights();
}

export function GetPageForTextOffset(offset) {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return null;

    const textLength = getTextLength(chapter);
    if (offset < 0 || offset >= textLength) return null;

    const range = createRangeFromOffsets(chapter, offset, offset + 1);
    const rect = range?.getClientRects()[0];
    if (!rect) return null;

    const style = getComputedStyle(chapter);
    const columnWidth = parseFloat(style.columnWidth);
    const columnGap = parseFloat(style.columnGap);
    const columnCount = Number.parseInt(style.getPropertyValue('--column-count'), 10) || 1;
    if (!Number.isFinite(columnWidth) || !Number.isFinite(columnGap) || columnWidth <= 0) return null;

    const columnIndex = Math.max(0, Math.floor(
        (rect.left - chapter.getBoundingClientRect().left) / (columnWidth + columnGap)));
    return Math.floor(columnIndex / columnCount) + 1;
}

export const SelectHighlightAt = (x, y) => {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return false;

    const rangeAtPoint = getHighlightRangeAtPoint(x, y);
    const offset = rangeAtPoint ? null : GetTextOffsetAt(x, y);
    const range = rangeAtPoint ?? highlightedRanges.find(highlightRange => {
        if (offset === null) return false;

        const start = getTextOffset(chapter, highlightRange.startContainer, highlightRange.startOffset);
        const end = getTextOffset(chapter, highlightRange.endContainer, highlightRange.endOffset);
        return offset >= start && offset < end;
    });
    if (!range) return false;

    const start = getTextOffset(chapter, range.startContainer, range.startOffset);
    const end = getTextOffset(chapter, range.endContainer, range.endOffset);
    if (!setSelection(start, end, true)) return false;
    editingHighlightRange = { start, end };
    editingHighlightChanged = false;
    debounceSelectionChange();
    return true;
};

const getHighlightRangeAtPoint = (x, y) => highlightedRanges.find(range =>
    Array.from(range.getClientRects()).some(rect =>
        x >= rect.left && x <= rect.right &&
        y >= rect.top && y <= rect.bottom));

export const GetTextOffsetAt = (x, y) => {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return null;

    const position = getCaretPositionAtPoint(x, y, chapter);
    return position ? getTextOffset(chapter, position.container, position.offset) : null;
};

export const HasTextSelection = () => {
    if (!activeSelection || !isSelectionRootCurrent()) return false;
    return activeSelection.anchorOffset !== activeSelection.focusOffset;
};

export const ClearTextSelection = () => {
    clearSelectionState();
    notifyNoSelection();
};

export const ConsumeSelectionCancellation = () => {
    const wasPending = selectionCancellationPending;
    selectionCancellationPending = false;
    clearTimeout(selectionCancellationTimeout);
    return wasPending;
};

export function ConsumeSelection(preserveWhitespace = false) {
    const range = getCurrentSelectionRange();
    if (!range) return null;

    const { startOffset, endOffset } = getSelectionOffsets();
    const selectedText = range.toString();
    const text = preserveWhitespace ? selectedText : selectedText.trim();
    clearSelectionState();
    notifyNoSelection();
    return text ? { startOffset, endOffset, text } : null;
}

export function GetSelectedText() {
    const range = getCurrentSelectionRange();
    return range?.toString() ?? null;
}

const debounceSelectionChange = () => {
    clearTimeout(selectionChangeTimeout);
    if (!HasTextSelection()) {
        notifyNoSelection();
        return;
    }

    selectionChangeTimeout = setTimeout(() => {
        if (selectionPointer !== null) {
            notifyNoSelection();
            return;
        }
        if (document.querySelector('.reader-overlay')) {
            notifyNoSelection();
            return;
        }

        const range = getCurrentSelectionRange();
        const chapter = document.querySelector('.chapter.current');
        if (!range || !chapter) {
            notifyNoSelection();
            return;
        }

        const text = range.toString().trim();
        if (!text) {
            notifyNoSelection();
            return;
        }

        const viewer = chapter.closest('.viewer');
        const bounds = viewer?.getBoundingClientRect();
        const focusAtStart = activeSelection.focusOffset < activeSelection.anchorOffset;
        const point = bounds
            ? getVisibleCaretPoint(chapter, activeSelection.focusOffset, bounds, focusAtStart)
            : null;
        const left = point?.x ?? window.innerWidth / 2;
        const top = point
            ? (point.y > 56 ? point.y - 48 : point.y + 8)
            : 56;
        const { startOffset, endOffset } = getSelectionOffsets();
        Reference.DotNet.invokeMethodAsync(
            'OnTextSelected',
            text,
            left,
            top,
            startOffset,
            endOffset,
            ...getEditingHighlightMetadata());
    }, selectionChangeDelay);
};

const clearEditingHighlightRange = () => {
    editingHighlightRange = null;
    editingHighlightChanged = false;
};

const getEditingHighlightMetadata = () => [
    editingHighlightRange !== null,
    editingHighlightRange?.start ?? 0,
    editingHighlightRange?.end ?? 0,
    editingHighlightChanged
];

const notifyNoSelection = () =>
    Reference.DotNet?.invokeMethodAsync(
        'OnTextSelected',
        null,
        0,
        0,
        0,
        0,
        ...getEditingHighlightMetadata());
