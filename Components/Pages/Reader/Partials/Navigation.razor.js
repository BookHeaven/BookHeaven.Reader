export function FindZoomableElement(x, y) {
    let elements = document.elementsFromPoint(x, y);
    let element = elements.find(e => e.classList.value.includes("zoomable"));
    if (element) {
        return element.attributes["src"] ? element.attributes["src"].value : element.attributes["href"].value;
    }
    return null;
}