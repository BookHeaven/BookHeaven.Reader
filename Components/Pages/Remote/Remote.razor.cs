using Microsoft.AspNetCore.Components;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Services;

namespace BookHeaven.Reader.Components.Pages.Remote;

public partial class Remote
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;
    [Inject] private IDatabaseService DatabaseService { get; set; } = null!;
    
    private List<Author>? _authors;

    private List<Book>? _books;
    private bool _canConnect = true;
    private List<Guid>? _deviceBooks = [];
    private List<Book>? _filteredBooks = [];
    private Guid? _selectedAuthor;
    private Filters _selectedFilter = Filters.All;
    private string? _serverUrl;

    protected override async Task OnInitializedAsync()
    {
        _serverUrl = AppStateService.ServerUrl;

        await GetData();
    }

    private async Task OnButtonClick()
    {
        await GetData();
    }

    private async Task GetData()
    {
        _canConnect = await ServerService.CanConnect(_serverUrl);
        if (!_canConnect) return;
        _books = (await ServerService.GetAllBooks())?.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name)
            .ThenBy(x => x.SeriesIndex).ToList();
        await FilterBooks();
        _authors = (await ServerService.GetAllAuthors())?.OrderBy(x => x.Name).ToList();
    }

    private async Task FilterChanged()
    {
        await FilterBooks();
    }

    private async Task FilterBooks()
    {
        if (_selectedFilter == Filters.All)
        {
            _filteredBooks = _books;
        }
        else if (_selectedFilter == Filters.Author && _selectedAuthor.HasValue)
        {
            _filteredBooks = _books?.Where(x => x.AuthorId == _selectedAuthor).OrderBy(x => x.Author?.Name)
                .ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
        }
        else if (_selectedFilter == Filters.Missing)
        {
            await GetDownloadedBooks();
            if (_deviceBooks == null || _deviceBooks.Count == 0) return;
            _filteredBooks = _books?.Where(x => !_deviceBooks.Contains(x.BookId)).OrderBy(x => x.Author?.Name)
                .ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
        }
    }

    private async Task GetDownloadedBooks()
    {
        _deviceBooks = (await DatabaseService.GetAll<Book>())?.Select(x => x.BookId).ToList();
    }

    private enum Filters
    {
        All,
        Author,
        Missing
    }
}