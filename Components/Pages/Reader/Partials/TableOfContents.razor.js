export function ScrollToChapterInIndex(path) {
    let chapter = document.getElementById(path);
    if (!chapter) return false;
    chapter.scrollIntoView();
    return true;
}