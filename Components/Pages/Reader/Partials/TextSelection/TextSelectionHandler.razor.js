const bridge = { dotNet: null };
const highlights = { ranges: [], offsets: [] };
const TIMING = Object.freeze({
    pageTurn: 1200,
    selectionChange: 100,
    longPress: 500
});
const THRESHOLD = Object.freeze({ pointerMove: 4 });
const HANDLE = Object.freeze({
    height: 42,
    mouseHitboxHeight: 16,
    knobSize: 16,
    touchStemExtension: 6
});
const SelectionGesture = Object.freeze({
    PENDING: 'pending',
    HIGHLIGHT_PENDING: 'highlight-pending',
    HIGHLIGHT: 'highlight',
    HIGHLIGHT_CANCELLED: 'highlight-cancelled',
    WORD: 'word',
    WORD_DRAG: 'word-drag',
    TEXT: 'text',
    HANDLE: 'handle'
});
const PageTurnPhase = Object.freeze({
    IDLE: 'idle',
    SCHEDULED: 'scheduled',
    TURNING: 'turning'
});
const EditingHighlightPhase = Object.freeze({
    NONE: 'none',
    UNMODIFIED: 'unmodified',
    MODIFIED: 'modified'
});
const SelectionCancellationPhase = Object.freeze({
    INACTIVE: 'inactive',
    PENDING: 'pending'
});
const selection = {
    active: null,
    inputType: 'mouse',
    chapter: null,
    chapterFirstChild: null,
    editingHighlight: { range: null, phase: EditingHighlightPhase.NONE },
    acrossPages: false,
    pointer: null,
    changeTimeout: null,
    cancellation: { timeout: null, phase: SelectionCancellationPhase.INACTIVE },
    ui: {
        overlay: null,
        startHandle: null,
        endHandle: null,
        fallbackRects: []
    }
};
const pageTurn = {
    phase: PageTurnPhase.IDLE,
    timeout: null,
    direction: 0,
    blockedDirection: 0
};
const reader = {
    pageTextRangeCache: new Map(),
    pageStyleObserver: null,
    observedPageStyle: null,
    observedPageMarker: null,
    chapterMutationObserver: null
};
const pageTurnTransitions = new Map([
    [PageTurnPhase.IDLE, [PageTurnPhase.SCHEDULED]],
    [PageTurnPhase.SCHEDULED, [PageTurnPhase.IDLE, PageTurnPhase.TURNING]],
    [PageTurnPhase.TURNING, [PageTurnPhase.IDLE]]
]);
const selectionGestureTransitions = new Map([
    [SelectionGesture.PENDING, [SelectionGesture.TEXT, SelectionGesture.WORD]],
    [SelectionGesture.HIGHLIGHT_PENDING, [
        SelectionGesture.HIGHLIGHT,
        SelectionGesture.HIGHLIGHT_CANCELLED,
        SelectionGesture.WORD
    ]],
    [SelectionGesture.WORD, [SelectionGesture.WORD_DRAG]],
    [SelectionGesture.TEXT, []],
    [SelectionGesture.WORD_DRAG, []],
    [SelectionGesture.HANDLE, []],
    [SelectionGesture.HIGHLIGHT, []],
    [SelectionGesture.HIGHLIGHT_CANCELLED, []]
]);

const transitionSelectionGesture = nextMode => {
    const currentMode = selection.pointer?.mode;
    if (!selectionGestureTransitions.get(currentMode)?.includes(nextMode)) {
        throw new Error(`Invalid selection gesture transition: ${currentMode} -> ${nextMode}`);
    }

    selection.pointer.mode = nextMode;
};

const isWaitingForLongPress = pointer =>
    pointer?.mode === SelectionGesture.PENDING ||
    pointer?.mode === SelectionGesture.HIGHLIGHT_PENDING;

const transitionPageTurn = nextPhase => {
    if (!pageTurnTransitions.get(pageTurn.phase)?.includes(nextPhase)) {
        throw new Error(`Invalid page turn transition: ${pageTurn.phase} -> ${nextPhase}`);
    }

    pageTurn.phase = nextPhase;
};

export function Initialize(dotnet, startHandleLabel, endHandleLabel) {
    bridge.dotNet = dotnet;
    createSelectionOverlay(startHandleLabel, endHandleLabel);
    window.getSelection()?.removeAllRanges();
    updateSelectionEventListeners(true);
    refreshHighlights();
    observeReaderLayout();
}

export function Dispose() {
    updateSelectionEventListeners(false);
    clearTimeout(selection.changeTimeout);
    clearTimeout(selection.cancellation.timeout);
    clearSelectionPageTurnTimer();
    clearTimeout(selection.pointer?.longPressTimeout);
    reader.pageStyleObserver?.disconnect();
    reader.chapterMutationObserver?.disconnect();
    selection.active = null;
    selection.chapter = null;
    selection.chapterFirstChild = null;
    clearEditingHighlightRange();
    selection.pointer = null;
    selection.cancellation.phase = SelectionCancellationPhase.INACTIVE;
    selection.acrossPages = false;
    pageTurn.blockedDirection = 0;
    selection.ui.overlay?.remove();
    selection.ui.overlay = null;
    selection.ui.startHandle = null;
    selection.ui.endHandle = null;
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
    selection.ui.overlay = document.createElement('div');
    Object.assign(selection.ui.overlay.style, {
        position: 'fixed',
        inset: '0',
        zIndex: '999',
        pointerEvents: 'none'
    });

    selection.ui.startHandle = createSelectionHandle(startHandleLabel);
    selection.ui.endHandle = createSelectionHandle(endHandleLabel);
    selection.ui.overlay.append(selection.ui.startHandle, selection.ui.endHandle);
    document.body.append(selection.ui.overlay);
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
        height: `${HANDLE.mouseHitboxHeight}px`,
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
        width: `${HANDLE.knobSize}px`,
        height: `${HANDLE.knobSize}px`,
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
    reader.pageStyleObserver?.disconnect();
    reader.observedPageStyle = Array.from(document.querySelectorAll('style'))
        .find(style => style.textContent.includes('--chapter-page')) ?? null;
    reader.observedPageMarker = getPageMarker();
    if (reader.observedPageStyle) {
        reader.pageStyleObserver = new MutationObserver(handleReaderLayoutChange);
        reader.pageStyleObserver.observe(reader.observedPageStyle, {
            childList: true,
            characterData: true,
            subtree: true
        });
    }

    observeCurrentChapter();
};

const observeCurrentChapter = () => {
    reader.chapterMutationObserver?.disconnect();
    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return;

    reader.chapterMutationObserver = new MutationObserver(() => {
        reader.pageTextRangeCache.clear();
        if (selection.chapter && selection.chapterFirstChild !== selection.chapter.firstChild) {
            clearSelectionState();
            notifyNoSelection();
            return;
        }
        renderSelection();
    });
    reader.chapterMutationObserver.observe(chapter, { childList: true });
};

const getPageMarker = () => {
    const chapter = document.querySelector('.chapter.current');
    return chapter
        ? getComputedStyle(chapter).getPropertyValue('--chapter-page').trim()
        : null;
};

const handleReaderLayoutChange = () => {
    reader.pageTextRangeCache.clear();
    const pageMarker = getPageMarker();
    const pageChanged = pageMarker !== reader.observedPageMarker;
    reader.observedPageMarker = pageMarker;
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
    const selectedRange = getCurrentSelectionRange();
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    const viewerBounds = viewer?.getBoundingClientRect();

    if (selectedRange && viewer && viewerBounds) {
        const visibleRects = getVisibleSelectionRects(selectedRange, viewerBounds);
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
            const focusAtStart = selection.active.focusOffset < selection.active.anchorOffset;
            const focusPoint = getVisibleCaretPoint(
                chapter,
                selection.active.focusOffset,
                viewerBounds,
                focusAtStart);
            if (focusPoint) anchorX = focusPoint.x;
            const spaceAbove = selectedBounds.top - viewerBounds.top;
            const spaceBelow = viewerBounds.bottom - selectedBounds.bottom - HANDLE.height;
            if (spaceAbove >= actionBounds.height + margin) {
                desiredTop = selectedBounds.top - actionBounds.height - margin;
            } else if (spaceBelow >= actionBounds.height + margin) {
                desiredTop = selectedBounds.bottom + HANDLE.height + margin;
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
    clearTimeout(selection.cancellation.timeout);
    selection.cancellation.timeout = null;
    selection.cancellation.phase = SelectionCancellationPhase.INACTIVE;
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
        selection.cancellation.phase = SelectionCancellationPhase.PENDING;
        selection.cancellation.timeout = setTimeout(() => {
            selection.cancellation.phase = SelectionCancellationPhase.INACTIVE;
            selection.cancellation.timeout = null;
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
    clearTimeout(selection.pointer?.longPressTimeout);
    selection.pointer = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
        originX: event.clientX,
        originY: event.clientY,
        anchorOffset: pointerCaret?.position
            ? getTextOffset(chapter, pointerCaret.position.container, pointerCaret.position.offset)
            : null,
        mode: isTouchingHighlight
            ? SelectionGesture.HIGHLIGHT_PENDING
            : SelectionGesture.PENDING,
        longPressTimeout: null
    };

    if (selection.pointer.anchorOffset !== null) {
        selection.pointer.longPressTimeout = setTimeout(
            () => selectLongPressAtPointer(selection.pointer.id),
            TIMING.longPress);
    }
    pageTurn.blockedDirection = 0;
};

const selectLongPressAtPointer = pointerId => {
    if (selection.pointer?.id !== pointerId || !isWaitingForLongPress(selection.pointer)) return;

    if (selection.pointer.mode === SelectionGesture.HIGHLIGHT_PENDING &&
        SelectHighlightAt(selection.pointer.x, selection.pointer.y)) {
        transitionSelectionGesture(SelectionGesture.HIGHLIGHT);
        return;
    }

    selectWordAtPointer(pointerId);
};

const selectWordAtPointer = pointerId => {
    if (selection.pointer?.id !== pointerId || !isWaitingForLongPress(selection.pointer) ||
        selection.pointer.anchorOffset === null) return;

    const chapter = document.querySelector('.chapter.current');
    if (!chapter) return;
    const word = getWordOffsets(chapter, selection.pointer.anchorOffset);
    if (!word) return;

    clearEditingHighlightRange();
    const focusOffset = constrainSelectionOutsideHighlights(word.start, word.end);
    if (focusOffset === word.start) return;
    transitionSelectionGesture(SelectionGesture.WORD);
    selection.pointer.anchorOffset = word.start;
    selection.pointer.wordStart = word.start;
    selection.pointer.wordEnd = focusOffset;
    setSelection(word.start, focusOffset, false);
    debounceSelectionChange();
};

const handleSelectionPointerMove = event => {
    if (event.pointerId !== selection.pointer?.id) return;

    selection.pointer.x = event.clientX;
    selection.pointer.y = event.clientY;
    if (selection.pointer.mode === SelectionGesture.HIGHLIGHT) return;

    if (isWaitingForLongPress(selection.pointer) ||
        selection.pointer.mode === SelectionGesture.WORD) {
        const wasHighlightPending =
            selection.pointer.mode === SelectionGesture.HIGHLIGHT_PENDING;
        const distance = Math.hypot(
            event.clientX - selection.pointer.originX,
            event.clientY - selection.pointer.originY);
        if (distance < THRESHOLD.pointerMove) return;

        clearTimeout(selection.pointer.longPressTimeout);
        if (wasHighlightPending) {
            transitionSelectionGesture(SelectionGesture.HIGHLIGHT_CANCELLED);
            return;
        }
        transitionSelectionGesture(selection.pointer.mode === SelectionGesture.WORD
            ? SelectionGesture.WORD_DRAG
            : SelectionGesture.TEXT);
    }

    if (selection.pointer.mode === SelectionGesture.HIGHLIGHT_CANCELLED) return;

    if (selection.pointer.mode === SelectionGesture.WORD_DRAG) {
        updateWordSelectionFocusAtPointer();
    } else {
        updateSelectionFocusAtPointer();
    }
    scheduleSelectionPageTurn();
};

const updateSelectionFocusAtPointer = () => {
    if (!selection.pointer || selection.pointer.anchorOffset === null) return;
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!chapter || !viewer) return;

    const position = getPointerPosition(
        selection.pointer.mode === SelectionGesture.HANDLE
            ? selection.pointer.x - selection.pointer.caretOffsetX
            : selection.pointer.x,
        selection.pointer.mode === SelectionGesture.HANDLE
            ? selection.pointer.y - selection.pointer.caretOffsetY
            : selection.pointer.y,
        chapter,
        viewer);
    if (!position) return;

    const focusOffset = getTextOffset(chapter, position.container, position.offset);
    setSelection(
        selection.pointer.anchorOffset,
        constrainSelectionFocus(selection.pointer.anchorOffset, focusOffset),
        selection.acrossPages);
    debounceSelectionChange();
};

const constrainEditedHighlightFocus = (anchorOffset, focusOffset) => {
    if (!selection.editingHighlight.range) return focusOffset;

    const otherHighlights = highlights.offsets.filter(highlight =>
        highlight.start !== selection.editingHighlight.range.start ||
        highlight.end !== selection.editingHighlight.range.end);
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

    if (anchorOffset <= selection.editingHighlight.range.start ||
        anchorOffset >= selection.editingHighlight.range.end) return focusOffset;

    if (focusOffset < anchorOffset) return Math.min(focusOffset, selection.editingHighlight.range.start);
    if (focusOffset > anchorOffset) return Math.max(focusOffset, selection.editingHighlight.range.end);
    return selection.active.focusOffset;
};

const constrainSelectionOutsideHighlights = (anchorOffset, focusOffset) => {
    if (highlights.offsets.some(highlight =>
        anchorOffset >= highlight.start && anchorOffset < highlight.end)) {
        return anchorOffset;
    }

    if (focusOffset > anchorOffset) {
        const boundary = highlights.offsets
            .filter(highlight => anchorOffset <= highlight.start && focusOffset > highlight.start)
            .reduce((nearest, highlight) => Math.min(nearest, highlight.start), Number.POSITIVE_INFINITY);
        return Number.isFinite(boundary) ? boundary : focusOffset;
    }

    if (focusOffset < anchorOffset) {
        const boundary = highlights.offsets
            .filter(highlight => anchorOffset >= highlight.end && focusOffset < highlight.end)
            .reduce((nearest, highlight) => Math.max(nearest, highlight.end), Number.NEGATIVE_INFINITY);
        return Number.isFinite(boundary) ? boundary : focusOffset;
    }

    return focusOffset;
};

const constrainSelectionFocus = (anchorOffset, focusOffset) =>
    selection.editingHighlight.range
        ? constrainEditedHighlightFocus(anchorOffset, focusOffset)
        : constrainSelectionOutsideHighlights(anchorOffset, focusOffset);

const updateWordSelectionFocusAtPointer = () => {
    if (!selection.pointer || selection.pointer.anchorOffset === null) return;
    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!chapter || !viewer) return;

    const position = getPointerPosition(
        selection.pointer.x,
        selection.pointer.y,
        chapter,
        viewer);
    if (!position) return;

    const pointerOffset = getTextOffset(chapter, position.container, position.offset);
    const word = getWordOffsets(chapter, pointerOffset);
    let focusOffset = selection.pointer.wordEnd;
    if (pointerOffset < selection.pointer.wordStart && word?.start < selection.pointer.wordStart) {
        focusOffset = word.start;
    } else if (pointerOffset >= selection.pointer.wordEnd && word?.end > selection.pointer.wordEnd) {
        focusOffset = word.end;
    }

    setSelection(
        selection.pointer.wordStart,
        constrainSelectionOutsideHighlights(selection.pointer.wordStart, focusOffset),
        selection.acrossPages);
    debounceSelectionChange();
};

const handleSelectionPointerUp = event => {
    if (event.pointerId !== selection.pointer?.id) return;

    clearTimeout(selection.pointer.longPressTimeout);
    const selectionWasCreated = HasTextSelection();
    selection.pointer = null;
    pageTurn.blockedDirection = 0;
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
        {
            handle: selection.ui.startHandle,
            point: getTextCaretPoint(selection.chapter, startOffset, true),
            isStart: true
        },
        {
            handle: selection.ui.endHandle,
            point: getTextCaretPoint(selection.chapter, endOffset, false),
            isStart: false
        }
    ].filter(({ point, isStart }) =>
        point &&
        x >= point.x - 18 &&
        x <= point.x + 18 &&
        Math.abs(y - getSelectionHandleKnobCenterY(point, isStart, true)) <= HANDLE.height / 2);

    candidates.sort((first, second) => {
        const firstCenterY = getSelectionHandleKnobCenterY(first.point, first.isStart, true);
        const secondCenterY = getSelectionHandleKnobCenterY(second.point, second.isStart, true);
        return (x - first.point.x) ** 2 + (y - firstCenterY) ** 2 -
            ((x - second.point.x) ** 2 + (y - secondCenterY) ** 2);
    });
    return candidates[0]?.handle ?? null;
};

const getSelectionHandleKnobCenterY = (point, isStart, isTouch) => {
    const stemExtension = isTouch ? HANDLE.touchStemExtension : 0;
    return isStart
        ? point.y - point.lineHeight - stemExtension - HANDLE.knobSize / 2
        : point.y + stemExtension + HANDLE.knobSize / 2;
};

const beginSelectionHandleDrag = (event, handle) => {
    event.preventDefault();
    event.stopPropagation();
    if (!isSelectionRootCurrent() || !HasTextSelection()) return;

    setSelectionInputType(event.pointerType);
    const { startOffset, endOffset } = getSelectionOffsets();
    const startPoint = getTextCaretPoint(selection.chapter, startOffset, true);
    const endPoint = getTextCaretPoint(selection.chapter, endOffset, false);
    const isTouch = event.pointerType === 'touch';
    const startDistance = startPoint
        ? (event.clientX - startPoint.x) ** 2 +
        (event.clientY - getSelectionHandleKnobCenterY(startPoint, true, isTouch)) ** 2
        : Number.POSITIVE_INFINITY;
    const endDistance = endPoint
        ? (event.clientX - endPoint.x) ** 2 +
        (event.clientY - getSelectionHandleKnobCenterY(endPoint, false, isTouch)) ** 2
        : Number.POSITIVE_INFINITY;
    const isStartHandle = startDistance === endDistance
        ? handle === selection.ui.startHandle
        : startDistance < endDistance;
    const handlePoint = isStartHandle ? startPoint : endPoint;
    const anchorOffset = isStartHandle ? endOffset : startOffset;
    const focusOffset = isStartHandle ? startOffset : endOffset;
    setSelection(
        anchorOffset,
        constrainSelectionFocus(anchorOffset, focusOffset),
        selection.acrossPages);
    clearTimeout(selection.pointer?.longPressTimeout);
    selection.pointer = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
        originX: event.clientX,
        originY: event.clientY,
        anchorOffset,
        caretOffsetX: handlePoint ? event.clientX - handlePoint.x : 0,
        caretOffsetY: handlePoint ? event.clientY - handlePoint.y : 0,
        mode: SelectionGesture.HANDLE,
        longPressTimeout: null
    };
};

const moveSelectionHandleWithKeyboard = (event, handle) => {
    if ((event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') || !HasTextSelection()) return;
    event.preventDefault();

    setSelectionInputType('mouse');
    const { startOffset, endOffset } = getSelectionOffsets();
    const isStartHandle = handle === selection.ui.startHandle;
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
        selection.acrossPages);
    debounceSelectionChange();
};

const clearSelectionPageTurnTimer = () => {
    clearTimeout(pageTurn.timeout);
    pageTurn.timeout = null;
    pageTurn.direction = 0;
    if (pageTurn.phase === PageTurnPhase.SCHEDULED) {
        transitionPageTurn(PageTurnPhase.IDLE);
    }
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
    startOffset: Math.min(selection.active.anchorOffset, selection.active.focusOffset),
    endOffset: Math.max(selection.active.anchorOffset, selection.active.focusOffset)
});

const getCurrentSelectionRange = () => {
    if (!HasTextSelection() || !isSelectionRootCurrent()) return null;
    const { startOffset, endOffset } = getSelectionOffsets();
    return createRangeFromOffsets(selection.chapter, startOffset, endOffset);
};

const isSelectionRootCurrent = () => {
    if (!selection.active) return false;
    const chapter = document.querySelector('.chapter.current');
    if (chapter === selection.chapter && chapter?.firstChild === selection.chapterFirstChild) return true;

    clearSelectionState();
    notifyNoSelection();
    return false;
};

const setSelection = (anchorOffset, focusOffset, acrossPages) => {
    const chapter = document.querySelector('.chapter.current');
    if (!chapter || !getPositionAtTextOffset(chapter, anchorOffset) ||
        !getPositionAtTextOffset(chapter, focusOffset)) return false;

    if (selection.editingHighlight.range) {
        const startOffset = Math.min(anchorOffset, focusOffset);
        const endOffset = Math.max(anchorOffset, focusOffset);
        selection.editingHighlight.phase =
            startOffset !== selection.editingHighlight.range.start ||
            endOffset !== selection.editingHighlight.range.end
                ? EditingHighlightPhase.MODIFIED
                : EditingHighlightPhase.UNMODIFIED;
    }
    selection.active = { anchorOffset, focusOffset };
    selection.chapter = chapter;
    selection.chapterFirstChild = chapter.firstChild;
    selection.acrossPages = acrossPages;
    renderSelection();
    return true;
};

const clearSelectionState = () => {
    selection.active = null;
    selection.chapter = null;
    selection.chapterFirstChild = null;
    clearEditingHighlightRange();
    selection.acrossPages = false;
    clearTimeout(selection.pointer?.longPressTimeout);
    selection.pointer = null;
    clearTimeout(selection.changeTimeout);
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
        selection.ui.overlay.insertBefore(highlight, selection.ui.startHandle);
        selection.ui.fallbackRects.push(highlight);
    }
};

const clearFallbackSelectionRects = () => {
    for (const rect of selection.ui.fallbackRects) rect.remove();
    selection.ui.fallbackRects = [];
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
    selection.ui.startHandle.style.display = 'none';
    selection.ui.endHandle.style.display = 'none';

    if (showStartHandle) setHandlePosition(selection.ui.startHandle, startPoint, true);
    if (showEndHandle) setHandlePosition(selection.ui.endHandle, endPoint, false);
};

const isPointWithinBounds = (point, bounds) =>
    point.x >= bounds.left &&
    point.x <= bounds.right &&
    point.y >= bounds.top &&
    point.y <= bounds.bottom;

const setHandlePosition = (handle, point, isStart) => {
    const [stem, knob] = handle.children;
    const isTouch = selection.inputType === 'touch';
    const handleTop = point.y - HANDLE.mouseHitboxHeight / 2;
    const stemExtension = isTouch ? HANDLE.touchStemExtension : 0;
    const stemTop = point.y - point.lineHeight - handleTop -
        (isStart ? stemExtension : 0);
    stem.style.top = `${stemTop}px`;
    stem.style.height = `${point.lineHeight + stemExtension}px`;
    knob.style.top = `${isStart
        ? stemTop - HANDLE.knobSize
        : stemTop + point.lineHeight + stemExtension}px`;
    handle.style.left = `${point.x - 18}px`;
    handle.style.top = `${handleTop}px`;
    handle.style.display = 'block';
};

const setSelectionInputType = pointerType => {
    const inputType = pointerType === 'touch' ? 'touch' : 'mouse';
    if (selection.inputType === inputType) return;

    selection.inputType = inputType;
    if (HasTextSelection()) renderSelection();
};

const hideSelectionHandles = () => {
    if (selection.ui.startHandle) selection.ui.startHandle.style.display = 'none';
    if (selection.ui.endHandle) selection.ui.endHandle.style.display = 'none';
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
    if (![SelectionGesture.TEXT, SelectionGesture.WORD_DRAG, SelectionGesture.HANDLE]
        .includes(selection.pointer?.mode) ||
        document.querySelector('.reader-overlay') ||
        !HasTextSelection()) return 0;

    const chapter = document.querySelector('.chapter.current');
    const viewer = chapter?.closest('.viewer');
    if (!viewer) return 0;

    const bounds = viewer.getBoundingClientRect();
    if (selection.pointer.y < bounds.top || selection.pointer.y >= bounds.bottom) return 0;

    const isForward = selection.active.focusOffset >= selection.active.anchorOffset;
    const page = Number.parseInt(getPageMarker(), 10);
    if (!Number.isFinite(page)) return 0;
    const pageTextRange = getSelectionPageTextOffsetRange(chapter, page);
    if (!pageTextRange || !isFocusNearPageTextBoundary(
        chapter,
        bounds,
        pageTextRange,
        selection.active.focusOffset,
        isForward)) return 0;

    const edgeZone = Math.min(40, bounds.width / 4);
    if (isForward && selection.pointer.x > bounds.right - edgeZone) return 1;
    if (!isForward && selection.pointer.x < bounds.left + edgeZone) return -1;
    return 0;
};

const getSelectionPageTextOffsetRange = (chapter, page) => {
    if (reader.pageTextRangeCache.has(page)) {
        return reader.pageTextRangeCache.get(page);
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
    reader.pageTextRangeCache.set(page, pageTextRange);
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
    if (pageTurn.phase === PageTurnPhase.TURNING) return;

    const direction = getSelectionPageTurnDirection();
    if (direction === 0) {
        pageTurn.blockedDirection = 0;
        clearSelectionPageTurnTimer();
        return;
    }
    if (pageTurn.blockedDirection === direction) return;
    pageTurn.blockedDirection = 0;
    if (pageTurn.timeout && pageTurn.direction === direction) return;

    clearSelectionPageTurnTimer();
    pageTurn.direction = direction;
    pageTurn.timeout = setTimeout(() => {
        pageTurn.timeout = null;
        turnSelectionPage(direction);
    }, TIMING.pageTurn);
    transitionPageTurn(PageTurnPhase.SCHEDULED);
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
    if (pageTurn.phase !== PageTurnPhase.SCHEDULED) return;

    transitionPageTurn(PageTurnPhase.TURNING);
    const chapterBeforeTurn = document.querySelector('.chapter.current');
    const pageBeforeTurn = getPageMarker();
    const firstChildBeforeTurn = chapterBeforeTurn?.firstChild;
    let shouldContinue = true;
    try {
        const pageChanged = await bridge.dotNet.invokeMethodAsync('OnSelectionPageTurn', direction);
        pageTurn.blockedDirection = direction;
        if (!pageChanged) {
            shouldContinue = false;
            return;
        }

        await waitForSelectionPageRender(chapterBeforeTurn, pageBeforeTurn, firstChildBeforeTurn);
        if (!isSelectionRootCurrent()) {
            shouldContinue = false;
            return;
        }

        selection.acrossPages = true;
        if (!selection.pointer) {
            shouldContinue = false;
            return;
        }
        if (selection.pointer.mode === SelectionGesture.WORD_DRAG) {
            updateWordSelectionFocusAtPointer();
        } else {
            updateSelectionFocusAtPointer();
        }
    } catch (error) {
        shouldContinue = false;
        console.error('Unable to change page while selecting text.', error);
    } finally {
        transitionPageTurn(PageTurnPhase.IDLE);
        pageTurn.direction = 0;
        if (shouldContinue) scheduleSelectionPageTurn();
    }
};

const refreshHighlights = () => {
    if (!window.CSS?.highlights || typeof Highlight === 'undefined') return;
    CSS.highlights.set('bookheaven-highlight', new Highlight(...highlights.ranges));
};

export function RestoreHighlights(annotations) {
    highlights.ranges.length = 0;
    highlights.offsets.length = 0;
    const chapter = document.querySelector('.chapter.current');
    if (chapter) {
        for (const highlight of annotations) {
            const range = createRangeFromOffsets(chapter, highlight.startOffset, highlight.endOffset);
            if (range) {
                highlights.ranges.push(range);
                highlights.offsets.push({ start: highlight.startOffset, end: highlight.endOffset });
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
    const range = rangeAtPoint ?? highlights.ranges.find(highlightRange => {
        if (offset === null) return false;

        const start = getTextOffset(chapter, highlightRange.startContainer, highlightRange.startOffset);
        const end = getTextOffset(chapter, highlightRange.endContainer, highlightRange.endOffset);
        return offset >= start && offset < end;
    });
    if (!range) return false;

    const start = getTextOffset(chapter, range.startContainer, range.startOffset);
    const end = getTextOffset(chapter, range.endContainer, range.endOffset);
    if (!setSelection(start, end, true)) return false;
    selection.editingHighlight.range = { start, end };
    selection.editingHighlight.phase = EditingHighlightPhase.UNMODIFIED;
    debounceSelectionChange();
    return true;
};

const getHighlightRangeAtPoint = (x, y) => highlights.ranges.find(range =>
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
    if (!selection.active || !isSelectionRootCurrent()) return false;
    return selection.active.anchorOffset !== selection.active.focusOffset;
};

export const ClearTextSelection = () => {
    clearSelectionState();
    notifyNoSelection();
};

export const ConsumeSelectionCancellation = () => {
    const wasPending = selection.cancellation.phase === SelectionCancellationPhase.PENDING;
    selection.cancellation.phase = SelectionCancellationPhase.INACTIVE;
    clearTimeout(selection.cancellation.timeout);
    selection.cancellation.timeout = null;
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
    clearTimeout(selection.changeTimeout);
    if (!HasTextSelection()) {
        notifyNoSelection();
        return;
    }

    selection.changeTimeout = setTimeout(() => {
        if (selection.pointer !== null) {
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
        const focusAtStart = selection.active.focusOffset < selection.active.anchorOffset;
        const point = bounds
            ? getVisibleCaretPoint(chapter, selection.active.focusOffset, bounds, focusAtStart)
            : null;
        const left = point?.x ?? window.innerWidth / 2;
        const top = point
            ? (point.y > 56 ? point.y - 48 : point.y + 8)
            : 56;
        const { startOffset, endOffset } = getSelectionOffsets();
        bridge.dotNet.invokeMethodAsync(
            'OnTextSelected',
            text,
            left,
            top,
            startOffset,
            endOffset,
            ...getEditingHighlightMetadata());
    }, TIMING.selectionChange);
};

const clearEditingHighlightRange = () => {
    selection.editingHighlight.range = null;
    selection.editingHighlight.phase = EditingHighlightPhase.NONE;
};

const getEditingHighlightMetadata = () => [
    selection.editingHighlight.phase !== EditingHighlightPhase.NONE,
    selection.editingHighlight.range?.start ?? 0,
    selection.editingHighlight.range?.end ?? 0,
    selection.editingHighlight.phase === EditingHighlightPhase.MODIFIED
];

const notifyNoSelection = () =>
    bridge.dotNet?.invokeMethodAsync(
        'OnTextSelected',
        null,
        0,
        0,
        0,
        0,
        ...getEditingHighlightMetadata());
