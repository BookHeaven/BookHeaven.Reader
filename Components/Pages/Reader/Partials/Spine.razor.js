export function ScrollToChapterInIndex(path) {
    let chapter = document.getElementById(path);
    console.log(chapter);
    chapter.scrollIntoView();
}