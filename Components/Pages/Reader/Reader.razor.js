let Reference = { DotNet: null };
let mainChapter = null;
let viewer = null;
let chapterContents = null;
let stylesheetIndices = null;
let countQueue = [];
let isCounting = false;


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

/**
 * Guarda los contenidos de los capítulos para los recuentos posteriores,
 * evitando reenviarlos por JS interop en cada recuento.
 */
export function SetChapterData(contents, indices) {
    chapterContents = contents;
    stylesheetIndices = indices;
}

export async function WaitForAssetsToLoad() {
    // Todo acotado: un asset que nunca dispare load/error (o una fuente colgada)
    // no puede bloquear al lector para siempre
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }
    await waitForImagesToLoad(mainChapter);
}


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

                let url;
            try {
                url = new URL(href, base).href;
            } catch {
                return; // src malformado: se ignora
            }
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

/**
 * Mueve los capítulos indicados al principio de la cola de medición.
 * Si el conteo ya está en marcha, el bucle recoge el reordenado en su
 * siguiente paso, lo que permite priorizar bajo demanda (p. ej. el capítulo
 * al que ha saltado el usuario) sin reiniciar el conteo.
 * Un capítulo que no esté en cola pero con el conteo activo se añade
 * igualmente (p. ej. invalidado por un cambio de ajustes a mitad de conteo).
 */
export function Prioritize(chapters) {
    if (!chapterContents || !chapters?.length) return;
    for (const chapter of chapters) {
        if (chapter < 0 || chapter >= chapterContents.length) continue;
        const index = countQueue.indexOf(chapter);
        if (index > 0) {
            countQueue.splice(index, 1);
            countQueue.unshift(chapter);
        } else if (index === -1 && isCounting) {
            countQueue.unshift(chapter);
        }
    }
}

/**
 * Mide el número de páginas de los capítulos indicados (por índice) y notifica
 * a .NET el resultado de CADA capítulo (OnChapterCounted), esperándolo para
 * garantizar que .NET lo haya procesado antes de continuar.
 *
 * Los índices se añaden a una cola dinámica: si ya hay un conteo en marcha,
 * el bucle en curso los recoge respetando cualquier reordenado previo
 * (Prioritize), en lugar de lanzar un segundo conteo en paralelo.
 */
export async function CountChapters(indices) {
    if (!chapterContents || !indices.length) return;
    for (const i of indices) {
        if (i < 0 || i >= chapterContents.length) continue;
        if (!countQueue.includes(i)) countQueue.push(i);
    }
    if (isCounting) return; // el bucle en curso recogerá la cola
    isCounting = true;
    try {
        await countQueuedChapters();
    } finally {
        isCounting = false;
    }
}

async function countQueuedChapters() {
    if (!countQueue.length) return;

    // Re-localizar por si Blazor ha re-renderizado el DOM
    viewer = document.querySelector('#viewer');
    mainChapter = document.querySelector('.chapter.current');
    if (!viewer || !mainChapter) {
        console.warn('[Reader] CountChapters: no hay #viewer o .chapter.current en el DOM');
        return; // la cola se conserva: la recogerá el próximo CountChapters
    }

    // Esperar a que las fuentes estén listas (acotado: si se cuelgan, se mide igual)
    if (document.fonts && document.fonts.ready) {
        await document.fonts.ready;
    }

    // Pre-cargar en paralelo las imágenes de los capítulos a contar (acotado)
    const prefetched = await prefetchChapterImages(countQueue.map(i => chapterContents[i]));

    // Calcular el ancho de página real (incluye gap)
    const computedStyle = getComputedStyle(mainChapter);
    const pageGap = parseFloat(computedStyle.columnGap) || 0;
    const pageWidth = Math.max(mainChapter.offsetWidth + pageGap, 1);

    // Crear un contenedor de medición invisible, pero con layout, para medir cada capítulo
    const measureEl = document.createElement('div');
    measureEl.className = 'chapter measure-target';
    measureEl.style.cssText = 'visibility:hidden;';
    viewer.appendChild(measureEl);

    try {
        // Cola dinámica: un Prioritize() llegado a mitad de conteo reordena
        // los capítulos restantes, así el siguiente en medirse es el priorizado
        while (countQueue.length) {
            const i = countQueue.shift();
            try {
                console.log(`[Reader] Midiendo capítulo ${i}...`);
                // Insertar el HTML del capítulo en el contenedor de medición
                measureEl.innerHTML = chapterContents[i];

                // Aplicar stylesheets si hay
                measureEl.className = 'chapter measure-target' +
                    (stylesheetIndices[i] || []).map(s => ' stylesheet-' + s).join('');

                // Esperar a que las imágenes se carguen (acotado: ya vienen pre-cargadas)
                await waitForImagesToLoad(measureEl, prefetched);

                // Forzar layout (reflow) para que scrollWidth sea correcto
                void measureEl.offsetWidth;

                // Medir y notificar a .NET (esperar a que lo haya procesado)
                const pageCount = measureChapter(measureEl, pageWidth, pageGap);
                measureEl.innerHTML = '';
                await Reference.DotNet.invokeMethodAsync('OnChapterCounted', i, pageCount);
            } catch (e) {
                // Un capítulo problemático no puede tumbar todo el lote
                console.error(`[Reader] Error midiendo el capítulo ${i}:`, e);
                measureEl.innerHTML = '';
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
}

export function Dispose() {
    window.removeEventListener('keydown', keyDownHandler);
}