using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Enums;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Extensions;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Books;

public partial class Books
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private BookManager BookManager { get; set; } = null!;

    private Book? _selectedBook;

    private bool _initialized;

    protected override async Task OnInitializedAsync()
    {
        if (AppStateService.ProfileId == Guid.Empty) return;
        await BookManager.GetBooks(AppStateService.ProfileId);
        if(BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.Reading))
        {
            BookManager.Filter = BookStatus.Reading;
        }
        else if (BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.New))
        {
            BookManager.Filter = BookStatus.New;
        }
        else if (BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.Finished))
        {
            BookManager.Filter = BookStatus.Finished;
        }
        else
        {
            BookManager.Filter = BookStatus.All;
        }
        _initialized = true;
        StateHasChanged();
    }
}