using System.ComponentModel;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Features.ProfileSettingss;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BookHeaven.Reader.Services;

public partial class ReaderService(
    AppStateService appStateService,
    ISender sender,
    ILogger<ReaderService> logger) : IDisposable
{
    public ProfileSettings ProfileSettings { get; set; } = null!;
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

    public async Task Initialize()
    {
        var getSettings = await sender.Send(new GetProfileSettings.Query(appStateService.ProfileId));
        ProfileSettings = getSettings.IsSuccess ? getSettings.Value : new() {ProfileId = appStateService.ProfileId};
        if (ProfileSettings.ProfileSettingsId == Guid.Empty)
        {
            await sender.Send(new AddProfileSettings.Command(ProfileSettings));
        }
        
        ProfileSettings.PropertyChanged += OnProfileSettingsChanged;
    }
    
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
        logger.LogInformation($"Navigated to chapter {chapter} and page {page}");

        
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
            //OnShouldRecalculateTotalPages?.Invoke();
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
            //OnShouldRecalculateTotalPages?.Invoke();
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

    private async void OnProfileSettingsChanged(object? s, PropertyChangedEventArgs e)
    {
        await sender.Send(new UpdateProfileSettings.Command(ProfileSettings));
    }

    public void Dispose()
    {
        ProfileSettings.PropertyChanged -= OnProfileSettingsChanged;
        GC.SuppressFinalize(this);
    }
}