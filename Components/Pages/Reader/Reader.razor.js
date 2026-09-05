let Reference = { DotNet: null };
let mainChapter = null;
let pageWidth = null;
let pageGap = null;


const waitForImagesToLoad = async (element = document.querySelector("#viewer")) => {
    const images = Array.from(element.querySelectorAll('img, svg image'))
        .filter((image) => !image.hasAttribute('width') && !image.hasAttribute('height'));
    if (!images.length) {
        await new Promise((resolve) => requestAnimationFrame(() => resolve()));
        return;
    }

    await Promise.all(images.map(async (image) => {
        if (image.tagName.toLowerCase() === 'image') {
            // <image> dentro de SVG no dispara eventos load/error, se espera a través de una Image temporal
            const src = image.getAttribute('href') || image.getAttributeNS('http://www.w3.org/1999/xlink', 'href');
            if (!src) return;

            const temp = new Image();
            await new Promise((resolve) => {
                temp.onload = temp.onerror = () => resolve();
                temp.src = src;
            });
            return;
        }

        if (image.complete) return;

        await new Promise((resolve) => {
            const onDone = () => {
                image.removeEventListener('load', onDone);
                image.removeEventListener('error', onDone);
                resolve();
            };

            image.addEventListener('load', onDone, { once: true });
            image.addEventListener('error', onDone, { once: true });
        });
    }));

    await new Promise((resolve) => requestAnimationFrame(() => resolve()));
};

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}

export async function Initialize(dotnet) {
    Reference.DotNet = dotnet;
    
    mainChapter = document.querySelector('.chapter.current');

    window.addEventListener('keydown', keyDownHandler);
}

export async function WaitForAssetsToLoad() {
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    await waitForImagesToLoad(mainChapter);
}

export async function GetPageCount() {
    const chapters = Array.from(document.querySelectorAll('.chapter.invisible'));
    if (!chapters.length) {
        return [];
    }

    pageGap = Number.parseFloat(getComputedStyle(mainChapter).columnGap);
    pageWidth = Math.max(mainChapter.offsetWidth + pageGap, 1);
    
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    await waitForImagesToLoad();
    return chapters.map((chapter) => Math.max(1, Math.ceil(chapter.scrollWidth / pageWidth)));
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}