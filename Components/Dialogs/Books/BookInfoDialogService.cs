namespace BookHeaven.Reader.Components.Dialogs.Books;

public sealed class BookInfoDialogService
{
    public event Action<Book>? OnShow;
    
    public void Show(Book book)
    {
        OnShow?.Invoke(book);
    }
}