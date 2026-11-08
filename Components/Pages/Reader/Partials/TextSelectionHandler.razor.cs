using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Enums;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public sealed partial class TextSelectionHandler : IAsyncDisposable
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private IReaderAnnotationService AnnotationService { get; set; } = null!;
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private DotNetObjectReference<TextSelectionHandler>? _dotNetReference;
    private IJSObjectReference? _module;
    private bool _isSelectionActionVisible;
    private bool _selectionHasHighlight;
    private bool _selectionCanHighlight;
    private bool _selectionCanCombine;
    private bool _selectionActionPositioned;
    private bool _restoreHighlightsOnNextRender;
    private bool _isEditingHighlightModified;
    private bool _isNoteEditorVisible;
    private BookAnnotation? _editingHighlight;
    private BookAnnotation? _noteAnnotation;
    private double _selectionActionLeft;
    private double _selectionActionTop;

    protected override void OnInitialized()
    {
        AnnotationService.OnHighlightsChanged += OnHighlightsChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_module is null && ReaderService.IsCurrentChapterLoaded)
        {
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Partials/TextSelectionHandler.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                "Initialize",
                _dotNetReference,
                Translations.SELECTION_START,
                Translations.SELECTION_END);
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
        await _module.InvokeVoidAsync("RestoreHighlights", AnnotationService.GetAnnotationsForCurrentChapter());
    }

    public async Task<int?> GetPageForTextOffsetAsync(int offset)
    {
        if (_module is null) return null;
        return await _module.InvokeAsync<int?>("GetPageForTextOffset", offset);
    }

    [JSInvokable("OnTextSelected")]
    public Task OnTextSelected(
        string? text,
        double left,
        double top,
        int startOffset,
        int endOffset,
        bool isEditingHighlight,
        int originalStartOffset,
        int originalEndOffset,
        bool isEditingHighlightModified)
    {
        _isSelectionActionVisible = !string.IsNullOrWhiteSpace(text);
        _selectionActionPositioned = false;
        _selectionActionLeft = left;
        _selectionActionTop = top;
        if (isEditingHighlight)
        {
            if (_editingHighlight is null ||
                _editingHighlight.StartOffset != originalStartOffset ||
                _editingHighlight.EndOffset != originalEndOffset)
            {
                var existing = AnnotationService.GetAnnotationForRange(originalStartOffset, originalEndOffset);
                _editingHighlight = existing is not null &&
                                   existing.StartOffset == originalStartOffset &&
                                   existing.EndOffset == originalEndOffset
                    ? existing.Clone()
                    : null;
            }

            _isEditingHighlightModified = _editingHighlight is not null && isEditingHighlightModified;
        }
        else
        {
            _editingHighlight = null;
            _isEditingHighlightModified = false;
        }

        _selectionHasHighlight = _isSelectionActionVisible && !_isEditingHighlightModified &&
                                 AnnotationService.HasRemovableAnnotationsInRange(startOffset, endOffset);
        _selectionCanHighlight = _isSelectionActionVisible && !_isEditingHighlightModified &&
                                 AnnotationService.HasUnhighlightedInRange(startOffset, endOffset);
        _selectionCanCombine = _isSelectionActionVisible && !isEditingHighlight &&
                              AnnotationService.HasAdjacentAnnotations(startOffset, endOffset);
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable("OnSelectionPageTurn")]
    public bool OnSelectionPageTurn(int direction)
    {
        var state = ReaderService.State;
        if (state is null) return false;

        switch (direction)
        {
            case 1:
            {
                if (state.PageNumber >= state.TotalPages) return false;
                ReaderService.NextPage();
                break;
            }
            case -1:
            {
                if (state.PageNumber <= 1) return false;
                ReaderService.PreviousPage();
                break;
            }
            default:
                return false;
        }

        return true;
    }

    private async Task ApplyHighlight()
    {
        await UpdateHighlightsFromSelectionAsync(range =>
            AnnotationService.AddAnnotationAsync(range.StartOffset, range.EndOffset, range.Text));
    }

    private async Task CombineHighlights()
    {
        await UpdateHighlightsFromSelectionAsync(range =>
            AnnotationService.CombineAdjacentAnnotationsAsync(range.StartOffset, range.EndOffset, range.Text));
    }

    private async Task RemoveHighlight()
    {
        await UpdateHighlightsFromSelectionAsync(range =>
            AnnotationService.RemoveAnnotationsAsync(range.StartOffset, range.EndOffset));
    }

    private async Task ApplyEditedHighlight()
    {
        if (_module is null || _editingHighlight is null) return;

        var annotation = _editingHighlight.Clone();
        var range = await _module.InvokeAsync<HighlightRange?>("ConsumeSelection", true);
        _isSelectionActionVisible = false;
        _editingHighlight = null;
        _isEditingHighlightModified = false;
        if (range is null) return;

        annotation.StartOffset = range.StartOffset;
        annotation.EndOffset = range.EndOffset;
        annotation.Text = range.Text;
        var result = await AnnotationService.UpdateAnnotationRangeAsync(annotation);
        if (result.IsFailure)
        {
            await AlertService.ShowToastAsync(result.Error.Description, AlertSeverity.Error);
        }
    }

    private async Task CopySelection()
    {
        if (_module is null) return;

        var text = await _module.InvokeAsync<string?>("GetSelectedText");
        if (string.IsNullOrEmpty(text)) return;

        await Clipboard.SetTextAsync(text);
        _isSelectionActionVisible = false;
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
        _editingHighlight = null;
        _isEditingHighlightModified = false;
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

        var existingAnnotation = AnnotationService.GetAnnotationForRange(range.StartOffset, range.EndOffset);
        _noteAnnotation = existingAnnotation?.Clone() ?? new BookAnnotation
        {
            ChapterIndex = ReaderService.State?.ChapterNumber ?? 0,
            ChapterTitle = ReaderService.State?.ChapterTitle ?? string.Empty,
            StartOffset = range.StartOffset,
            EndOffset = range.EndOffset,
            Text = range.Text
        };
        _isSelectionActionVisible = false;
        _isNoteEditorVisible = true;
    }

    private async Task SaveNote(BookAnnotation annotation)
    {
        var result = await AnnotationService.SaveAnnotationAsync(annotation);
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
        AnnotationService.OnHighlightsChanged -= OnHighlightsChanged;
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
