using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.Profiles;
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

    private List<Book>? _books;
    private bool _canConnect = true;
    private List<Guid>? _deviceBooks = [];
    private List<Book>? _filteredBooks;
    private List<Book> CurrentPageBooks => _filteredBooks?.Skip((_currentPage - 1) * ItemsPerPage).Take(ItemsPerPage).ToList() ?? [];
    private Filters _selectedFilter = Filters.Missing;

    protected override async Task OnInitializedAsync()
    {
        await GetData();
        /*var getFonts = await Sender.Send(new GetAllFonts.Query());
        if (getFonts is { IsSuccess: true, Value.Count: 0 })
        {
            _ = ServerService.DownloadFonts();
        }*/
    }

    private async Task OnReconnectButtonClick()
    {
        await GetData();
    }

    private async Task GetData()
    {
        _canConnect = (await ServerService.CanConnect()).IsSuccess;
        if (!_canConnect) return;
        
        var getRemoteProfiles = await ServerService.GetAllProfiles();
        if (getRemoteProfiles.IsSuccess)
        {
            var getLocalProfiles = await Sender.Send(new GetAllProfiles.Query());
            if (getLocalProfiles.IsSuccess)
            {
                foreach (var profile in getLocalProfiles.Value)
                {
                    var remoteProfile = getRemoteProfiles.Value.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
                    if (remoteProfile is null || remoteProfile.Name == profile.Name) continue;
                    
                    await Sender.Send(new UpdateProfileName.Command(profile.ProfileId, remoteProfile.Name));
                    if (profile.ProfileId == AppStateService.ProfileId)
                    {
                        AppStateService.OnProfileNameChanged?.Invoke(remoteProfile.Name);
                    }
                }
            }
        }
        
        var getBooks = await ServerService.GetAllBooks();
        if (getBooks.IsFailure)
        {
            _canConnect = false;
            return;
        }
        _books = getBooks.Value.OrderBy(x => x.Author?.Name).ThenBy(x => x.Series?.Name).ThenBy(x => x.SeriesIndex).ToList();
        await FilterBooks();
    }

    private async Task FilterChanged()
    {
        await FilterBooks();
    }

    private async Task FilterBooks()
    {
        switch (_selectedFilter)
        {
            case Filters.All:
                _filteredBooks = _books;
                break;
            case Filters.Missing:
            {
                await GetDownloadedBooks();
                _filteredBooks = _books?.Where(x => _deviceBooks?.Contains(x.BookId) == false).OrderBy(x => x.Author?.Name)
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
        var getBooks = await Sender.Send(new GetAllBooks.Query());
        if (getBooks.IsSuccess)
        {
            _deviceBooks = getBooks.Value.Select(x => x.BookId).ToList();
        }
    }

    private enum Filters
    {
        All,
        Missing
    }
}