import { SelectHighlightAt } from './TextSelectionHandler.razor.js';

let dotNetReference;
let buttons = [];
let hadSelectionOnMouseDown = false;

export function Initialize(dotNet, layout) {
    dotNetReference = dotNet;
    buttons = layout;
    window.addEventListener('keydown', handleKeyDown);
    document.addEventListener('mousedown', handleReaderMouseDown, true);
    document.addEventListener('click', handleReaderClick);
    document.addEventListener('dblclick', handleReaderDoubleClick, true);
    document.addEventListener('contextmenu', handleContextMenu);
}

export function UpdateLayout(layout) {
    buttons = layout;
}

export function Dispose() {
    window.removeEventListener('keydown', handleKeyDown);
    document.removeEventListener('mousedown', handleReaderMouseDown, true);
    document.removeEventListener('click', handleReaderClick);
    document.removeEventListener('dblclick', handleReaderDoubleClick, true);
    document.removeEventListener('contextmenu', handleContextMenu);
    dotNetReference = null;
    buttons = [];
    hadSelectionOnMouseDown = false;
}

function handleKeyDown(event) {
    if (event.repeat) return;
    dotNetReference?.invokeMethodAsync('OnKeyDown', event.key);
}

export function FindZoomableElement(x, y) {
    let elements = document.elementsFromPoint(x, y);
    let element = elements.find(e => e.classList.value.includes("zoomable"));
    if (element) {
        return element.attributes["src"] ? element.attributes["src"].value : element.attributes["href"].value;
    }
    return null;
}

function handleReaderClick(event) {
    const shouldSuppressNavigation = event.detail > 0 && hadSelectionOnMouseDown;
    hadSelectionOnMouseDown = false;
    if (!event.target.closest('.reader-surface')) return;

    if (event.detail === 2) {
        event.preventDefault();
        window.getSelection()?.removeAllRanges();
    }

    if (shouldSuppressNavigation) return;

    const selection = window.getSelection();
    if (selection && !selection.isCollapsed) return;

    const column = Math.min(2, Math.floor(event.clientX / window.innerWidth * 3));
    const row = Math.min(2, Math.floor(event.clientY / window.innerHeight * 3));
    const buttonIndex = row * 3 + column;
    if (buttonIndex < buttons.length) {
        dotNetReference?.invokeMethodAsync('OnNavigationTap', buttonIndex);
    }
}

function handleReaderMouseDown(event) {
    const isReaderSurface = event.target.closest('.reader-surface') !== null;
    const selection = window.getSelection();
    hadSelectionOnMouseDown = isReaderSurface && selection !== null && !selection.isCollapsed;

    if (event.detail < 2 || !isReaderSurface) return;

    event.preventDefault();
    window.getSelection()?.removeAllRanges();
}

function handleReaderDoubleClick(event) {
    if (!event.target.closest('.reader-surface')) return;
    event.preventDefault();
    window.getSelection()?.removeAllRanges();
}

function handleContextMenu(event) {
    if (!event.target.closest('.reader-surface')) return;
    const data = FindZoomableElement(event.clientX, event.clientY);
    if (data) {
        event.preventDefault();
        dotNetReference?.invokeMethodAsync('OnOpenImageViewer', data);
        return;
    }
    // Long-pressing (or right-clicking) a highlight selects it so the action
    // popup offers to remove it, without interfering with tap navigation.
    if (SelectHighlightAt(event.clientX, event.clientY)) {
        event.preventDefault();
    }
}