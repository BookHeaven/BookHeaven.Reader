using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Extensions;
using CommunityToolkit.Maui.Alerts;

namespace BookHeaven.Reader.Services;

public class BookManager(IDatabaseService databaseService)
{
    public List<Book> Books { get; set; } = [];

    public async Task ClearCache(Book book, bool showToast = true)
    {
        var progress = book.GetCachePath(CacheKey.Progress);
        var styles = book.GetCachePath(CacheKey.Styles);

        if (File.Exists(progress))
        {
            File.Delete(progress);
        }

        if (File.Exists(styles))
        {
            File.Delete(styles);
        }
        if(showToast) await Toast.Make("Cache cleared").Show();
    }
    
    public async Task ResetProgress(Book book)
    {
        var progress = new BookProgress()
        {
            BookProgressId = book!.Progress().BookProgressId,
            ProfileId = book!.Progress().ProfileId,
            BookId = book!.BookId,
        };
        await databaseService.AddOrUpdate(progress);
        await databaseService.SaveChanges();

        await ClearCache(book, false);
        await Toast.Make("Progress has been reset").Show();
    }
    
    public async Task DeleteBook(Book book)
    {
        var epubPath = book!.GetEpubPath();
        var coverPath = book!.GetCoverPath();
        
        if (File.Exists(epubPath))
        {
            File.Delete(epubPath);
        }
        if (File.Exists(coverPath))
        {
            File.Delete(coverPath);
        }
        await ClearCache(book, false);
        await databaseService.Delete<Book>(book.BookId);
        Books.Remove(book);
        await Toast.Make("Book has been deleted").Show();
    }
}