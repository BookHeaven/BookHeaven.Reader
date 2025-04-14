using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Features.BooksProgress;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Extensions;
using CommunityToolkit.Maui.Alerts;
using MediatR;

namespace BookHeaven.Reader.Services;

public class BookManager(
    AlertService alertService,
    ISender sender)
{
    public List<Book> Books { get; set; } = [];

    private async Task ClearCache(Book book, bool showToast = true)
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
    
    public async Task DeleteBook(Book book)
    {
        var result = await alertService.ShowConfirmationAsync("Delete book", $"Are you sure you want to delete this book?{Environment.NewLine}{Environment.NewLine}This will remove the book from your device along with any progress you have.{Environment.NewLine}It will not be removed from your server.");
        if (!result) return;
        
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
    
    public async Task MarkAsNew(Book book)
    {
        var result = await alertService.ShowConfirmationAsync("Are you sure?", "This will reset your progress, which can't be undone unless you delete the book and download it again.");
        if (!result) return;
        
        var progress = book.Progress();
        progress.StartDate = null;
        progress.EndDate = null;
        progress.Progress = 0;
        progress.ElapsedTime = TimeSpan.Zero;
        progress.LastRead = null;
        progress.Chapter = 0;
        progress.Page = 0;
        progress.PageCount = 0;
        progress.PageCountNext = 0;
        progress.PageCountPrev = 0;
        await sender.Send(new UpdateBookProgress.Command(progress));
        await ClearCache(book, false);
        await Toast.Make("Book has been marked as new").Show();
    }
    
    public async Task MarkAsFinished(Book book)
    {
        var progress = book.Progress();
        if(progress.ElapsedTime == TimeSpan.Zero)
        {
            progress.StartDate = DateTimeOffset.Now;
        }
        progress.EndDate = DateTimeOffset.Now;
        progress.Progress = 100;
        await sender.Send(new UpdateBookProgress.Command(progress));
        await ClearCache(book, false);
        await Toast.Make("Book has been marked as finished").Show();
    }
}