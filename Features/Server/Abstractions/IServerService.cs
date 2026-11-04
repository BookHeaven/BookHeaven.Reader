using BookHeaven.Core.Shared;

namespace BookHeaven.Reader.Features.Server.Abstractions;

public interface IServerService
{
    Task<Result> CanConnect();
    Task<Result<List<Book>>> GetAllBooks();
    Task<Result<List<Author>>> GetAllAuthors();
    Task<Result<List<Profile>>> GetAllProfiles();
    Task<Result<BookProgress?>> GetBookProgress(Guid profileId, Guid bookId);
    Task<Result<List<BookProgress>>> GetAllBooksProgress(Guid profileId);
    Task<Result> DownloadBook(Book book, Guid profileId);
    Task<Result> UpdateBookProgress(BookProgress progress);
    Task<Result> SyncProfile(Guid profileId);
    Task<Result> UpdateProfileSettings(ProfileSettings settings);
    Task<Result> UpdateLocalProfiles();
    Task<Result> DownloadFonts();
}