using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
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
    [Inject] private ISender Sender { get; set; } = null!;
    [Inject] private BookManager BookManager { get; set; } = null!;

    private enum Filter
    {
        [StringValue(nameof(Translations.READING))]
        Reading,
        [StringValue(nameof(Translations.ALL_M))]
        All
    }

    private List<Book> FilteredBooks => _selectedFilter switch
    {
        Filter.Reading => BookManager.Books.GetReadingBooks(),
        Filter.All => BookManager.Books.ToList()
    };
    private Filter _selectedFilter;
    
    private Book? _selectedBook;

    private bool _initialized;

    protected override async Task OnInitializedAsync()
    {
        if (AppStateService.ProfileId == Guid.Empty) return;
        var getBooks = await Sender.Send(new GetAllBooks.Query(AppStateService.ProfileId));
        if (getBooks.IsSuccess)
        {
            BookManager.Books = getBooks.Value;
        }
  
        _selectedFilter = BookManager.Books.AnyReading() ? Filter.Reading : Filter.All;
        _initialized = true;
    }
}