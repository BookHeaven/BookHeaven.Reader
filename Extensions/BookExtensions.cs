using BookHeaven.EbookManager;
using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Extensions;

public static class BookExtensions
{
	extension(Book book)
	{
		public string CachePath(CacheKey key) => Path.Combine(EbookManagerGlobals.CachePath, $"{book.BookId}-{key}.cache");

		public string CoverUrlWithCustomScheme => BookHeavenScheme.BuildUrl(book.CoverUrl());
	}
}