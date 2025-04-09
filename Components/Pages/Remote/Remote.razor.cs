using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.Fonts;
using BookHeaven.Reader.Services;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Remote;

public partial class Remote
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;

    private const int ItemsPerPage = 6;
    private int _currentPage = 1;

    private List<Author>? _authors;

    private List<Book>? _books;
    private bool _canConnect = true;
    private List<Guid>? _deviceBooks = [];
    private List<Book>? _filteredBooks;
    private List<Book> CurrentPageBooks => _filteredBooks?.Skip((_currentPage - 1) * ItemsPerPage).Take(ItemsPerPage).ToList() ?? [];
    private Guid? _selectedAuthor;
    private Filters _selectedFilter = Filters.All;

    protected override async Task OnInitializedAsync()
    {
        await GetData();
        var getFonts = await Sender.Send(new GetAllFonts.Query());
        if (getFonts is { IsSuccess: true, Value.Count: 0 })
        {
            _ = ServerService.DownloadFonts();
        }
    }

    private async Task OnButtonClick()
    {
        await GetData();
    }

    private async Task GetData()
    {
        _canConnect = await ServerService.CanConnect();
        if (!_canConnect) return;
        _books = (await ServerService.GetAllBooks())?.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name)
            .ThenBy(x => x.SeriesIndex).ToList();
        await FilterBooks();
        _authors = (await ServerService.GetAllAuthors())?.OrderBy(x => x.Name).ToList();
    }

    private async Task FilterChanged()
    {
        if(_selectedFilter == Filters.Author && _selectedAuthor == null)
            return;
        await FilterBooks();
    }

    private async Task FilterBooks()
    {
        switch (_selectedFilter)
        {
            case Filters.All:
                _filteredBooks = _books;
                break;
            case Filters.Author when _selectedAuthor.HasValue:
                _filteredBooks = _books?.Where(x => x.AuthorId == _selectedAuthor).OrderBy(x => x.Author?.Name)
                    .ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
                break;
            case Filters.Missing:
            {
                await GetDownloadedBooks();
                if (_deviceBooks == null || _deviceBooks.Count == 0) return;
                _filteredBooks = _books?.Where(x => !_deviceBooks.Contains(x.BookId)).OrderBy(x => x.Author?.Name)
                    .ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
        _currentPage = 1;
    }

    private async Task GetDownloadedBooks()
    {
        var getBooks = await Sender.Send(new GetAllBooks.Query(AppStateService.ProfileId));
        if (getBooks.IsSuccess)
        {
            _deviceBooks = getBooks.Value.Select(x => x.BookId).ToList();
        }
    }

    private enum Filters
    {
        All,
        Author,
        Missing
    }
}