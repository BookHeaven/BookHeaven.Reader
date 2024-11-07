using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Extensions;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Books;

public partial class Books
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IDatabaseService DatabaseService { get; set; } = null!;

    private enum Filter
    {
        [StringValue(nameof(Translations.READING))]
        Reading,
        [StringValue(nameof(Translations.ALL_M))]
        All
    }
    
    private List<Book> _books = [];
    private List<Book> _filteredBooks = [];
    private Filter _selectedFilter;
    
    private Book? _selectedBook;
    

    protected override async Task OnInitializedAsync()
    {
        if (AppStateService.ProfileId == Guid.Empty) return;
        _books = (await DatabaseService.GetAllIncluding<Book>(b => b.Author, b => b.Series,
                b => b.Progresses.Where(p => p.ProfileId == AppStateService.ProfileId)))
            ?.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList()!;
        _selectedFilter = _books.AnyReading() ? Filter.Reading : Filter.All;
        FilterBooks();
    }

    private void FilterBooks()
    {
        _filteredBooks = _selectedFilter switch
        {
            Filter.Reading => _books.GetReadingBooks(),
            Filter.All => _books.ToList(),
            _ => _filteredBooks
        };
    }
}