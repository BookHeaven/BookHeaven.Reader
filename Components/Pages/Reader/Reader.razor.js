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
    let pageWidth = page.offsetWidth + parseFloat(getComputedStyle(page).getPropertyValue('--page-gap'));

    const [pagesPrev, pages, pagesNext] = await Promise.all([
        pagePrev.childElementCount > 0 ? Math.ceil(parseFloat((pagePrev.scrollWidth / pageWidth).toFixed(1))) : 1,
        Math.ceil(parseFloat((page.scrollWidth / pageWidth).toFixed(1))),
        pageNext.childElementCount > 0 ? Math.ceil(parseFloat((pageNext.scrollWidth / pageWidth).toFixed(1))) : 1
    ]);

    return [pagesPrev - 1, pages - 1, pagesNext - 1];
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}