using BookHeaven.Domain.Entities;
using BookHeaven.EbookManager;
using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Extensions;

public static class BookExtensions
{
	public static string GetCachePath(this Book book, CacheKey key)
    {
        return Path.Combine(EbookManagerGlobals.CachePath, $"{book.BookId}-{key}.cache");
    }
}