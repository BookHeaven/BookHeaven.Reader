using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.BooksProgress;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Extensions;
using CommunityToolkit.Maui.Alerts;
using MediatR;

namespace BookHeaven.Reader.Services;

public class BookManager(ISender sender)
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
            BookProgressId = book.Progress().BookProgressId,
            ProfileId = book.Progress().ProfileId,
            BookId = book.BookId,
        };
        
        await sender.Send(new UpdateBookProgress.Command(progress));

        await ClearCache(book, false);
        await Toast.Make("Progress has been reset").Show();
    }
    
    public async Task DeleteBook(Book book)
    {
        var epubPath = book.EpubPath(MauiProgram.BooksPath);
        var coverPath = book.CoverPath(MauiProgram.CoversPath);
        
        if (File.Exists(epubPath))
        {
            File.Delete(epubPath);
        }
        if (File.Exists(coverPath))
        {
            File.Delete(coverPath);
        }
        await ClearCache(book, false);
        await sender.Send(new DeleteBook.Command(book.BookId));
        Books.Remove(book);
        await Toast.Make("Book has been deleted").Show();
    }
}