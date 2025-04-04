export function FindZoomableElement(x, y) {
    let elements = document.elementsFromPoint(x, y);
    let element = elements.find(e => e.className.includes("zoomable"));
    if (element) {
        return element.attributes["src"].value;
    }
    return null;
}