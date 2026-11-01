using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Entities;
using BookHeaven.Core.Enums;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class TextSelectionHandler : IAsyncDisposable
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private DotNetObjectReference<TextSelectionHandler>? _dotNetReference;
    private IJSObjectReference? _module;
    private bool _isSelectionActionVisible;
    private bool _selectionHasHighlight;
    private bool _selectionCanHighlight;
    private bool _selectionActionPositioned;
    private bool _restoreHighlightsOnNextRender;
    private bool _isNoteEditorVisible;
    private BookAnnotation? _noteAnnotation;
    private double _selectionActionLeft;
    private double _selectionActionTop;

    protected override void OnInitialized()
    {
        ReaderService.OnHighlightsChanged += OnHighlightsChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Partials/TextSelectionHandler.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("Initialize", _dotNetReference);
        }

        if (_module is null) return;

        if (_isSelectionActionVisible)
        {
            await _module.InvokeVoidAsync("ClampSelectionAction");
            if (!_selectionActionPositioned)
            {
                _selectionActionPositioned = true;
                StateHasChanged();
            }
        }

        if (_restoreHighlightsOnNextRender)
        {
            _restoreHighlightsOnNextRender = false;
            await RestoreHighlightsAsync();
        }
    }

    public async Task RestoreHighlightsAsync()
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("RestoreHighlights", ReaderService.GetAnnotationsForCurrentChapter());
    }

    public async Task<int?> GetPageForTextOffsetAsync(int offset)
    {
        if (_module is null) return null;
        return await _module.InvokeAsync<int?>("GetPageForTextOffset", offset);
    }

    [JSInvokable("OnTextSelected")]
    public Task OnTextSelected(string? text, double left, double top, int startOffset, int endOffset)
    {
        _isSelectionActionVisible = !string.IsNullOrWhiteSpace(text);
        _selectionActionPositioned = false;
        _selectionActionLeft = left;
        _selectionActionTop = top;
        _selectionHasHighlight = _isSelectionActionVisible && ReaderService.HasAnnotationsInRange(startOffset, endOffset);
        _selectionCanHighlight = _isSelectionActionVisible && ReaderService.HasUnhighlightedInRange(startOffset, endOffset);
        return InvokeAsync(StateHasChanged);
    }

    private async Task ApplyHighlight()
    {
        await UpdateHighlightsFromSelectionAsync(range =>
            ReaderService.AddAnnotationAsync(range.StartOffset, range.EndOffset, range.Text));
    }

    private async Task RemoveHighlight()
    {
        await UpdateHighlightsFromSelectionAsync(range =>
            ReaderService.RemoveAnnotationsAsync(range.StartOffset, range.EndOffset));
    }

    private async Task UpdateHighlightsFromSelectionAsync(Func<HighlightRange, Task<Result>> update)
    {
        if (_module is null) return;

        var range = await _module.InvokeAsync<HighlightRange?>("ConsumeSelection");
        Result? result = null;
        if (range is not null)
        {
            result = await update(range);
        }
        _isSelectionActionVisible = false;
        if (result is { IsFailure: true })
        {
            await AlertService.ShowToastAsync(result.Error.Description, AlertSeverity.Error);
        }
    }

    private async Task EditNote()
    {
        if (_module is null) return;

        var range = await _module.InvokeAsync<HighlightRange?>("ConsumeSelection");
        if (range is null) return;

        var existingAnnotation = ReaderService.GetAnnotationForRange(range.StartOffset, range.EndOffset);
        _noteAnnotation = new BookAnnotation
        {
            BookAnnotationId = existingAnnotation?.BookAnnotationId ?? Guid.Empty,
            ChapterIndex = existingAnnotation?.ChapterIndex ?? ReaderService.State?.ChapterNumber ?? 0,
            ChapterTitle = existingAnnotation?.ChapterTitle ?? ReaderService.State?.ChapterTitle ?? string.Empty,
            StartOffset = existingAnnotation?.StartOffset ?? range.StartOffset,
            EndOffset = existingAnnotation?.EndOffset ?? range.EndOffset,
            Text = existingAnnotation?.Text ?? range.Text,
            Note = existingAnnotation?.Note 
        };
        _isSelectionActionVisible = false;
        _isNoteEditorVisible = true;
    }

    private async Task SaveNote(BookAnnotation annotation)
    {
        var result = await ReaderService.SaveAnnotationAsync(annotation);
        if (result.IsFailure)
        {
            await AlertService.ShowToastAsync(result.Error.Description, AlertSeverity.Error);
            return;
        }

        CancelNote();
    }

    private void CancelNote()
    {
        _isNoteEditorVisible = false;
        _noteAnnotation = null;
    }

    private void OnHighlightsChanged()
    {
        _restoreHighlightsOnNextRender = true;
        InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        ReaderService.OnHighlightsChanged -= OnHighlightsChanged;
        _dotNetReference?.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("Dispose");
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        GC.SuppressFinalize(this);
    }

    private record HighlightRange(int StartOffset, int EndOffset, string Text);
}
