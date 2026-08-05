using BookHeaven.Domain.Features.Books;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Remote;

public partial class Remote
{
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private IServerService ServerService { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;

    private const int ItemsPerPage = 6;
    private int _currentPage = 1;

    private List<Book>? _books;
    private bool _canConnect = true;
    private string _connectError = string.Empty;
    private HashSet<Guid>? _deviceBooks = [];
    private IEnumerable<Book>? _filteredBooks;
    private List<Book> CurrentPageBooks => _filteredBooks?.Skip((_currentPage - 1) * ItemsPerPage).Take(ItemsPerPage).ToList() ?? [];
    private BookStatus _selectedBookStatus = BookStatus.Missing;
    private string _filter = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await GetData();
    }

    private async Task OnReconnectButtonClick()
    {
        await GetData();
    }

    private async Task GetData()
    {
        _connectError = string.Empty;
        var canConnect = await ServerService.CanConnect();
        if (canConnect.IsFailure)
        {
            _canConnect = false;
            _connectError = canConnect.Error.Description;
            return;
        }

        _ = ServerService.UpdateLocalProfiles();
        _ = ServerService.DownloadFonts();
        
        var getBooks = await ServerService.GetAllBooks();
        if (getBooks.IsFailure)
        {
            _canConnect = false;
            _connectError = getBooks.Error.Description;
            return;
        }
        _books = getBooks.Value.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
        await GetDownloadedBooks();
        FilterBooks();
    }

    private void FilterBooks()
    {
        switch (_selectedBookStatus)
        {
            case BookStatus.All:
                _filteredBooks = _books;
                break;
            case BookStatus.Missing:
            {
                _filteredBooks = _books?.Where(x => _deviceBooks?.Contains(x.BookId) == false);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        if(!string.IsNullOrEmpty(_filter))
        {
            _filteredBooks = _filteredBooks?
                .Where(x => x.Title?.Contains(_filter, StringComparison.OrdinalIgnoreCase) == true 
                            || x.Author?.Name?.Contains(_filter, StringComparison.OrdinalIgnoreCase) == true
                            || x.Series?.Name?.Contains(_filter, StringComparison.OrdinalIgnoreCase) == true);
        }

        _filteredBooks = _filteredBooks?.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
        
        _currentPage = 1;
    }

    private async Task GetDownloadedBooks()
    {
        var getBooks = await Sender.Send(new GetAllBooks.Query());
        if (getBooks.IsSuccess)
        {
            _deviceBooks = getBooks.Value.Select(x => x.BookId).ToHashSet();
        }
    }

    private void HandleBookDownloaded(Guid bookId)
    {
        _deviceBooks?.Add(bookId);
        FilterBooks();
    }

    private enum BookStatus
    {
        All,
        Missing
    }
}