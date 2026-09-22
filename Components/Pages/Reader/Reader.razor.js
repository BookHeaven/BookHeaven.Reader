const Reference = {
    DotNet: null,
};

export async function Initialize(dotnet) {
    Reference.DotNet = dotnet;

    window.addEventListener('keydown', keyDownHandler);
}

export function GetPageDimensions() {
    let mainChapter = document.querySelector('.chapter.current');
    return [
        mainChapter.offsetWidth,
        mainChapter.offsetHeight,
    ];
}

export async function WaitForAssetsToLoad() {
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    let mainChapter = document.querySelector('.chapter.current');
    await waitForImagesToLoad(mainChapter);
}

const waitForImagesToLoad = async (element = document.querySelector(".viewer"), prefetched = null) => {
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

        // Si no la hemos pre-cargado, el navegador ya la está cargando solo por estar en el DOM
        const src = image.currentSrc || image.src;
        if (prefetched && src && !prefetched.has(src)) return;

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

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}