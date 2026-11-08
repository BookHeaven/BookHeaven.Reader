using BookHeaven.Core.Abstractions;
using BookHeaven.Core.Enums;
using BookHeaven.Core.Features.Reader.Abstractions;
using BookHeaven.Core.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials.TextSelection;

[Flags]
internal enum SelectionAction : byte
{
    None = 0,
    Highlight = 1 << 0,
    RemoveHighlight = 1 << 1,
    Combine = 1 << 2
}

internal static class SelectionActionExtensions
{
    internal static bool Contains(this SelectionAction actions, SelectionAction action) =>
        (actions & action) == action;
}

public sealed partial class TextSelectionHandler : IAsyncDisposable
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private IReaderService ReaderService { get; set; } = null!;
    [Inject] private IReaderAnnotationService AnnotationService { get; set; } = null!;
    [Inject] private IAlertService AlertService { get; set; } = null!;

    private DotNetObjectReference<TextSelectionHandler>? _dotNetReference;
    private IJSObjectReference? _module;
    private SelectionActionState _selectionActionState;
    private SelectionAction _selectionActions;
    private bool _restoreHighlightsOnNextRender;
    private EditingHighlight? _editingHighlight;
    private BookAnnotation? _noteAnnotation;
    private double _selectionActionLeft;
    private double _selectionActionTop;

    private bool IsSelectionActionVisible => _selectionActionState is not SelectionActionState.Hidden;
    private bool IsSelectionActionPositioned => _selectionActionState is SelectionActionState.Positioned;
    private bool IsEditedHighlightModified =>
        _editingHighlight?.State is EditingHighlightState.Modified;
    private bool IsNoteEditorVisible => _noteAnnotation is not null;

    protected override void OnInitialized()
    {
        AnnotationService.OnHighlightsChanged += OnHighlightsChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_module is null)
        {
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Reader/Partials/TextSelection/TextSelectionHandler.razor.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync(
                "Initialize",
                _dotNetReference,
                Translations.SELECTION_START,
                Translations.SELECTION_END);
        }

        if (_module is null) return;

        if (IsSelectionActionVisible)
        {
            await _module.InvokeVoidAsync("ClampSelectionAction");
            if (_selectionActionState is SelectionActionState.AwaitingPosition)
            {
                _selectionActionState = SelectionActionState.Positioned;
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
        if (_module is null)
        {
            _restoreHighlightsOnNextRender = true;
            return;
        }

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
        _selectionActionState = string.IsNullOrWhiteSpace(text)
            ? SelectionActionState.Hidden
            : SelectionActionState.AwaitingPosition;
        _selectionActionLeft = left;
        _selectionActionTop = top;
        if (isEditingHighlight)
        {
            var annotation = _editingHighlight?.Annotation;
            if (annotation is null ||
                annotation.StartOffset != originalStartOffset ||
                annotation.EndOffset != originalEndOffset)
            {
                var existing = AnnotationService.GetAnnotationForRange(originalStartOffset, originalEndOffset);
                annotation = existing is not null &&
                             existing.StartOffset == originalStartOffset &&
                             existing.EndOffset == originalEndOffset
                    ? existing.Clone()
                    : null;
            }

            _editingHighlight = annotation is null
                ? null
                : new EditingHighlight(
                    annotation,
                    isEditingHighlightModified
                        ? EditingHighlightState.Modified
                        : EditingHighlightState.Unmodified);
        }
        else
        {
            _editingHighlight = null;
        }

        _selectionActions = !IsSelectionActionVisible || IsEditedHighlightModified
            ? SelectionAction.None
            : (AnnotationService.HasUnhighlightedInRange(startOffset, endOffset)
                ? SelectionAction.Highlight
                : SelectionAction.None)
              | (AnnotationService.HasRemovableAnnotationsInRange(startOffset, endOffset)
                  ? SelectionAction.RemoveHighlight
                  : SelectionAction.None)
              | (!isEditingHighlight && AnnotationService.HasAdjacentAnnotations(startOffset, endOffset)
                  ? SelectionAction.Combine
                  : SelectionAction.None);
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

        var annotation = _editingHighlight.Annotation.Clone();
        var range = await _module.InvokeAsync<HighlightRange?>("ConsumeSelection", true);
        HideSelectionAction();
        _editingHighlight = null;
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
        HideSelectionAction();
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
        HideSelectionAction();
        _editingHighlight = null;
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
        HideSelectionAction();
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
        _noteAnnotation = null;
    }

    private void HideSelectionAction() =>
        _selectionActionState = SelectionActionState.Hidden;

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
    private sealed record EditingHighlight(BookAnnotation Annotation, EditingHighlightState State);
    private enum SelectionActionState
    {
        Hidden,
        AwaitingPosition,
        Positioned
    }

    private enum EditingHighlightState
    {
        Unmodified,
        Modified
    }
}
