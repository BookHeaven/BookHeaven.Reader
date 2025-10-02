using BookHeaven.Domain.Abstractions;
using BookHeaven.Domain.Entities;
using BookHeaven.Domain.Enums;
using BookHeaven.Domain.Extensions;
using BookHeaven.Domain.Features.Books;
using BookHeaven.Domain.Services;
using BookHeaven.Reader.Extensions;
using BookHeaven.Reader.Resources.Localization;
using BookHeaven.Reader.Services;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Books;

public partial class Books
{
    [Inject] private IAlertService AlertService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private AppStateService AppStateService { get; set; } = null!;
    [Inject] private BookManager BookManager { get; set; } = null!;
    [Inject] private ISender Sender { get; set; } = null!;

    private Book? _selectedBook;

    private bool _initialized;

    protected override async Task OnInitializedAsync()
    {
        if (AppStateService.ProfileId == Guid.Empty) return;
        await BookManager.GetBooksAsync(AppStateService.ProfileId);
        if(BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.Reading))
        {
            BookManager.Filter = BookStatus.Reading;
        }
        else if (BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.New))
        {
            BookManager.Filter = BookStatus.New;
        }
        else if (BookManager.Books.Any(b => b.ReadingStatus() == BookStatus.Finished))
        {
            BookManager.Filter = BookStatus.Finished;
        }
        else
        {
            BookManager.Filter = BookStatus.All;
        }
        _initialized = true;
        StateHasChanged();
    }

    private async Task DeleteBook(Guid bookId)
    {
        var result = await AlertService.ShowConfirmationAsync("Delete book", $"Are you sure you want to delete this book?{Environment.NewLine}{Environment.NewLine}This will remove the book from your device along with any progress you have.{Environment.NewLine}It will not be removed from your server.");
        if (!result) return;
        try
        {
            await Sender.Send(new DeleteBook.Command(bookId));
        }
        catch (Exception ex)
        {
            await AlertService.ShowAlertAsync("Error deleting book", ex.Message);
            return;
        }
        
        await AlertService.ShowToastAsync(Domain.Localization.Translations.BOOK_DELETED);
        BookManager.RemoveBook(bookId);
    }
}