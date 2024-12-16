using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Extensions;

public static class BookExtensions
{
    public static string GetEpubPath(this Book book)
    {
        return Path.Combine(MauiProgram.BooksPath, $"{book.BookId}.epub");
    }
    public static string GetCoverPath(this Book book)
	{
		return Path.Combine(MauiProgram.CoversPath, $"{book.BookId}.jpg");
	}
	public static string GetCachePath(this Book book, CacheKey key)
    {
        return Path.Combine(MauiProgram.CachePath, $"{book.BookId}-{key}.cache");
    }
}