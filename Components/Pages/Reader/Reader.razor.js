let Reference = { DotNet: null };
let mainChapter = null;
let pageWidth = null;
let pageGap = null;


const waitForImagesToLoad = async () => {
    const images = Array.from(document.querySelectorAll('.chapter img, .chapter svg'));
    if (!images.length) {
        await new Promise((resolve) => requestAnimationFrame(() => resolve()));
        return;
    }

    await Promise.all(images.map(async (image) => {
        if (image.tagName === 'IMG') {
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
            return;
        }

        if (image.tagName === 'SVG') {
            await new Promise((resolve) => requestAnimationFrame(() => resolve()));
        }
    }));

    await new Promise((resolve) => requestAnimationFrame(() => resolve()));
};

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}

export function Initialize(dotnet) {
    Reference.DotNet = dotnet;
    
    mainChapter = document.querySelector('.chapter.current');
    pageGap = Number.parseFloat(getComputedStyle(mainChapter).columnGap);
    pageWidth = Math.max(mainChapter.offsetWidth + pageGap, 1);

    window.addEventListener('keydown', keyDownHandler);
}

export async function GetPageCount() {
    const chapters = Array.from(document.querySelectorAll('.chapter.invisible'));
    if (!chapters.length) {
        return [];
    }
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    await waitForImagesToLoad();
    return chapters.map((chapter) => Math.max(1, Math.ceil(chapter.scrollWidth / pageWidth)));
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}