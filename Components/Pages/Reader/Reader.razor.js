let page, pagePrev, pageNext;

let Reference = {};
Reference.DotNet = null;

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}

export function SetDotNetReference(dotnet) {
    Reference.DotNet = dotnet;

    page = document.getElementsByClassName('chapter current')[0];
    pagePrev = document.getElementsByClassName('chapter prev')[0];
    pageNext = document.getElementsByClassName('chapter next')[0];

    window.addEventListener('keydown', keyDownHandler);
}

export async function GetPageCount() {
    let pageWidth = page.offsetWidth;

    /*let pagesPrev = (pagePrev.childElementCount > 0 ? Math.round(parseFloat((pagePrev.scrollWidth / pageWidth).toFixed(1))) : null);
    let pages = Math.round(parseFloat((page.scrollWidth / pageWidth).toFixed(1)));
    let pagesNext = (pageNext.childElementCount > 0 ? Math.round(parseFloat((pageNext.scrollWidth / pageWidth).toFixed(1))) : null);*/

    const [pagesPrev, pages, pagesNext] = await Promise.all([
        pagePrev.childElementCount > 0 ? Math.round(parseFloat((pagePrev.scrollWidth / pageWidth).toFixed(1))) : null,
        Math.round(parseFloat((page.scrollWidth / pageWidth).toFixed(1))),
        pageNext.childElementCount > 0 ? Math.round(parseFloat((pageNext.scrollWidth / pageWidth).toFixed(1))) : null
    ]);

    return [pagesPrev, pages, pagesNext];
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}