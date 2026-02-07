namespace BookHeaven.Reader.Services;

public class ReaderService
{
    public int CurrentChapter { get; private set; }
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public int TotalPagesPrev { get; private set; }
    public int TotalPagesNext { get; private set; }
    public int TotalChapters { get; set; }
    
    public Action? OnPageChanged { get; set; }
    public Action? OnChapterChanged { get; set; }
    public Action? OnTotalPagesChanged { get; set; }
    public Action<string>? OnChapterSelected { get; set; }
    
    public void SetTotalPages(int totalPages, int? totalPagesPrev = null, int? totalPagesNext = null)
    {
        TotalPagesPrev = totalPagesPrev ?? 0;
        TotalPagesNext = totalPagesNext ?? 0;
        TotalPages = totalPages;
        OnTotalPagesChanged?.Invoke();
    }
    
    public void NavigateTo(int page, int chapter)
    {
        CurrentChapter = chapter;
        CurrentPage = page;

        
        OnChapterChanged?.Invoke();
    }

    public void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            OnPageChanged?.Invoke();
        }
        else
        {
            if(CurrentChapter == TotalChapters - 1) return;
            
            TotalPagesPrev = TotalPages;
            TotalPages = TotalPagesNext;
            TotalPagesNext = -1;
            
            CurrentPage = 0;
            CurrentChapter++;
            OnChapterChanged?.Invoke();
            OnTotalPagesChanged?.Invoke();
        }
    }
    
    public void PreviousPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
            OnPageChanged?.Invoke();
        }
        else
        {
            if(CurrentChapter == 0) return;
            
            TotalPagesNext = TotalPages;
            TotalPages = TotalPagesPrev;
            TotalPagesPrev = -1;
            
            CurrentPage = TotalPages;
            CurrentChapter--;
            OnChapterChanged?.Invoke();
            OnTotalPagesChanged?.Invoke();
        }
    }

    public void NextChapter()
    {
        if (CurrentChapter >= TotalChapters - 1) return;
        CurrentChapter++;
        CurrentPage = 0;
        OnChapterChanged?.Invoke();
    }
    
    public void PreviousChapter()
    {
        if (CurrentChapter <= 0) return;
        CurrentChapter--;
        CurrentPage = 0;
        OnChapterChanged?.Invoke();
    }
}