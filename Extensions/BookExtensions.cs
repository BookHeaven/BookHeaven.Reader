using BookHeaven.Domain.Entities;

namespace BookHeaven.Reader.Extensions;

public static class BookExtensions
{
    public static bool IsStartedReading(this Book book)
    {
        return book.Progresses.Any(x => x.ElapsedTime != TimeSpan.Zero && x.EndDate == null);
    }
    
    public static bool IsFinishedReading(this Book book)
    {
        return book.Progresses.Any(x => x.EndDate != null);
    }
    
    public static List<Book> GetReadingBooks(this IEnumerable<Book> books)
    {
        return books.Where(b => b.IsStartedReading()).ToList();
    }
    
    public static bool AnyReading(this IEnumerable<Book> books)
    {
        return books.Any(b => b.IsStartedReading());
    }
}