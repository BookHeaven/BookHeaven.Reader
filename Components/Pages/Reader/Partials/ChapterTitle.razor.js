let canvas;

function getCtx() {
    canvas ??= document.createElement("canvas");
    return canvas.getContext("2d");
}

export function measureTitle(container, textElement, pageElement, text) {
    const style = getComputedStyle(textElement);
    const ctx = getCtx();
    ctx.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;

    const containerWidth = container.offsetWidth;
    const paginationWidth = pageElement.offsetWidth;
    const gap = parseFloat(getComputedStyle(pageElement).marginLeft) || 0;
    const availableWidth = containerWidth - paginationWidth - gap;
    const fullTextWidth = ctx.measureText(text).width;

    return { availableWidth, fullTextWidth };
}

export function observeResize(container, callback) {
    const observer = new ResizeObserver(() => callback.invokeMethodAsync("OnResize"));
    observer.observe(container);
    return observer;
}