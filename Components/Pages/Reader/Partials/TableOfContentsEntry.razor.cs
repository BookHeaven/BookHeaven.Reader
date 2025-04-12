using EpubManager.Entities;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class TableOfContentsEntry
{
    private bool _collapsed = true;
    private bool _isSelected = false;

    private string? _lastSelectedChapterId = null;

    [Parameter] [EditorRequired] public EpubChapter EpubChapter { get; set; } = null!;

    [Parameter] [EditorRequired] public string? CurrentChapterId { get; set; }

    [Parameter] public EventCallback<string> OnChapterSelected { get; set; }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
    }

    protected override void OnInitialized()
    {
        if (EpubChapter.Chapters.Count > 0) _collapsed = !EpubChapter.ContainsChapter(CurrentChapterId!);
    }

    protected override void OnParametersSet()
    {
        if (EpubChapter.ItemId != null) _isSelected = CurrentChapterId == EpubChapter.ItemId;

        if (_isSelected && _lastSelectedChapterId != EpubChapter.ItemId)
        {
            _lastSelectedChapterId = EpubChapter.ItemId;
        }
    }
}