let Reference = {};
Reference.DotNet = null;

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
                const cleanUp = () => {
                    image.removeEventListener('load', onDone);
                    image.removeEventListener('error', onDone);
                    resolve();
                };
                const onDone = () => cleanUp();

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

export function SetDotNetReference(dotnet) {
    Reference.DotNet = dotnet;

    window.addEventListener('keydown', keyDownHandler);
}

export async function GetPageCount() {
    const chapters = Array.from(document.querySelectorAll('.chapter.invisible'));
    if (!chapters.length) {
        return [];
    }
    await waitForImagesToLoad();

    const firstChapter = chapters[0];
    const pageGap = Number.parseFloat(getComputedStyle(firstChapter).columnGap) || 0;
    const pageWidth = Math.max(firstChapter.offsetWidth + pageGap, 1);

    return chapters.map((chapter) => Math.max(1, Math.ceil(chapter.scrollWidth / pageWidth)));
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}