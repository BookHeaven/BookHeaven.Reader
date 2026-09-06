let Reference = { DotNet: null };
let mainChapter = null;
let viewer = null;


const waitForImagesToLoad = async (element = document.querySelector("#viewer"), prefetched = null) => {
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

const keyDownHandler = (e) => {
    if (e.repeat) return;
    Reference.DotNet.invokeMethodAsync('OnKeyDown', e.key);
}

/**
 * Da un respiro al hilo de UI para que procese eventos (paint, input, etc.)
 * Equivalente al yield que hace koreader entre pasos de renderizado.
 */
const yieldToUI = () => new Promise(resolve => setTimeout(resolve, 0));

/**
 * Mide el número de páginas de UN capítulo concreto.
 * El capítulo debe estar en el DOM y visible (o al menos con layout).
 */
const measureChapter = (chapterEl, pageWidth, pageGap) => {
    // scrollWidth con column-fill: auto da el ancho total "desplegado"
    const totalWidth = chapterEl.scrollWidth;
    return Math.max(1, Math.ceil(totalWidth / (pageWidth + pageGap)));
};

export async function Initialize(dotnet) {
    Reference.DotNet = dotnet;

    viewer = document.querySelector('#viewer');
    mainChapter = document.querySelector('.chapter.current');
    

    window.addEventListener('keydown', keyDownHandler);
}

export async function WaitForAssetsToLoad() {
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    await waitForImagesToLoad(mainChapter);
}

/*export async function GetPageCount() {
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
}*/

/**
 * Extrae los src de las imágenes de todos los capítulos (solo las que no tienen
 * width/height y por tanto bloquean el layout) y los pre-carga en paralelo con
 * un pool de peticiones, para que al medir cada capítulo ya estén en caché.
 *
 * @returns {Promise<Set<string>>} urls absolutas pre-cargadas
 */
const prefetchChapterImages = async (chapterContents, poolSize = 6) => {
    const sources = [];
    const seen = new Set();

    for (const html of chapterContents) {
        const doc = new DOMParser().parseFromString(html, 'text/html');
        // baseURI es de solo lectura: si el fragmento declara <base> se usa ese
        // (al insertarse se hoísta en el documento), si no, la base del documento principal
        const baseHref = doc.querySelector('base')?.getAttribute('href');
        const base = baseHref
            ? new URL(baseHref, document.baseURI).href
            : document.baseURI;

        doc.querySelectorAll('img, svg image')
            .forEach((image) => {
                if (image.hasAttribute('width') || image.hasAttribute('height')) return;

                const href = image.hasAttribute('href')
                    ? image.getAttribute('href')
                    : image.getAttributeNS('http://www.w3.org/1999/xlink', 'href');
                if (!href) return;

                const url = new URL(href, base).href;
                if (!seen.has(url)) {
                    seen.add(url);
                    sources.push(url);
                }
            });
    }

    if (!sources.length) return seen;

    let index = 0;
    const worker = async () => {
        while (index < sources.length) {
            const url = sources[index++];
            await new Promise((resolve) => {
                const temp = new Image();
                temp.onload = temp.onerror = () => resolve();
                temp.src = url;
            });
        }
    };
    await Promise.all(Array.from({ length: Math.min(poolSize, sources.length) }, worker));

    return seen;
};

export async function GetPageCount(chapterContents, stylesheetIndices) {
    // Esperar a que las fuentes estén listas (solo una vez)
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }

    // Pre-cargar en paralelo las imágenes de TODOS los capítulos
    const prefetched = await prefetchChapterImages(chapterContents);

    // Calcular el ancho de página real (incluye gap)
    const computedStyle = getComputedStyle(mainChapter);
    const pageGap = parseFloat(computedStyle.columnGap) || 0;
    const pageWidth = Math.max(mainChapter.offsetWidth + pageGap, 1);

    // Crear un contenedor de medición invisible, pero con layout, para medir cada capítulo
    const measureEl = document.createElement('div');
    measureEl.className = 'chapter measure-target';
    measureEl.style.cssText = 'visibility:hidden;';
    viewer.appendChild(measureEl);

    const results = [];
    const total = chapterContents.length;
    let lastReported = 0;

    try {
        for (let i = 0; i < total; i++) {
            // Insertar el HTML del capítulo en el contenedor de medición
            measureEl.innerHTML = chapterContents[i];

            // Aplicar stylesheets si hay
            measureEl.className = 'chapter measure-target' +
                (stylesheetIndices[i] || []).map(s => ' stylesheet-' + s).join('');

            // Esperar a que las imágenes se carguen (debería ser inmediato: ya están en caché)
            await waitForImagesToLoad(measureEl, prefetched);

            // Forzar layout (reflow) para que scrollWidth sea correcto
            void measureEl.offsetWidth;

            // Medir
            results.push(measureChapter(measureEl, pageWidth, pageGap));

            // Limpiar el contenedor (liberar memoria)
            measureEl.innerHTML = '';

            // Notificar progreso (throttleado: al menos 5% de avance, o al terminar)
            const progress = (i + 1) / total;
            if (i === total - 1 || progress - lastReported >= 0.05) {
                lastReported = progress;
                Reference.DotNet.invokeMethodAsync('OnCountingProgress', progress);
            }

            // ⭐ YIELD al hilo de UI entre cada capítulo
            // Esto permite que el browser procese paint, input, eventos...
            // Es la clave para no bloquear la interfaz.
            await yieldToUI();
        }
    } finally {
        // Limpiar el contenedor de medición
        viewer.removeChild(measureEl);
    }

    return results;
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}