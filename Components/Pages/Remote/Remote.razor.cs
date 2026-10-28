using BookHeaven.Core.Features.Books;
using BookHeaven.Core.Features.BooksProgress;
using BookHeaven.Core.Helpers;
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
    private List<Book> _recommendedBooks = [];
    private BookStatus _selectedBookStatus = BookStatus.Recommended;
    private string _filter = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await GetData();
    }

    private async Task OnReconnectButtonClick()
    {
        _canConnect = true;
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
        await GetRecommendations();
        if (_recommendedBooks.Count == 0)
        {
            _selectedBookStatus = BookStatus.Missing;
        }
        FilterBooks();
    }

    private async Task GetRecommendations()
    {
        var getLocalProgress = await Sender.Send(new GetAllBooksProgressByProfile.Query(AppStateService.ProfileId));
        var getRemoteProgress = await ServerService.GetAllBooksProgress(AppStateService.ProfileId);
        var remoteProgress = getRemoteProgress.IsSuccess
            ? getRemoteProgress.Value
            : await GetRemoteProgressPerBook();

        _recommendedBooks = BookRecommender.Recommend(
            _books ?? [],
            getLocalProgress.IsSuccess ? getLocalProgress.Value : [],
            remoteProgress,
            _deviceBooks);
    }

    // Fallback for servers without the bulk progress endpoint.
    private async Task<List<BookProgress>> GetRemoteProgressPerBook()
    {
        using var throttle = new SemaphoreSlim(8);
        var requests = (_books ?? []).Select(async book =>
        {
            await throttle.WaitAsync();
            try
            {
                var getProgress = await ServerService.GetBookProgress(AppStateService.ProfileId, book.BookId);
                return getProgress.IsSuccess ? getProgress.Value : null;
            }
            finally
            {
                throttle.Release();
            }
        });

        return (await Task.WhenAll(requests)).OfType<BookProgress>().ToList();
    }

    private void FilterBooks(bool resetPage = true)
    {
        switch (_selectedBookStatus)
        {
            case BookStatus.Recommended:
                _filteredBooks = _recommendedBooks.Where(x => _deviceBooks?.Contains(x.BookId) == false);
                break;
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

        if (_selectedBookStatus != BookStatus.Recommended || !string.IsNullOrEmpty(_filter))
        {
            _filteredBooks = _filteredBooks?.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex);
        }
        _filteredBooks = _filteredBooks?.ToList();

        _currentPage = resetPage ? 1 : Math.Clamp(_currentPage, 1, (int)Math.Ceiling(decimal.Divide(_filteredBooks?.Count() ?? 1, ItemsPerPage)));
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
        FilterBooks(false);
    }

    private enum BookStatus
    {
        Recommended,
        All,
        Missing
    }
}