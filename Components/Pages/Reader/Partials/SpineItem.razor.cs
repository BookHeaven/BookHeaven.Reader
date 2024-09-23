using EpubManager.Entities;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class SpineItem
{
    private bool _collapsed = true;
    private bool _isSelected = false;

    private string? _lastSelectedChapterPath = null;
    private bool _shouldScroll = false;

    [Parameter] [EditorRequired] public EpubChapter EpubChapter { get; set; } = null!;

    [Parameter] [EditorRequired] public string? CurrentChapterPath { get; set; }

    [Parameter] public EventCallback<string> OnChapterSelected { get; set; }

    [Parameter] public EventCallback<string> DoScroll { get; set; }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
    }

    protected override void OnInitialized()
    {
        if (EpubChapter.Chapters?.Count > 0) _collapsed = !EpubChapter.ContainsChapter(CurrentChapterPath!);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && _shouldScroll) DoScroll.InvokeAsync(EpubChapter.Path);
    }

    protected override void OnParametersSet()
    {
        _shouldScroll = false;
        if (EpubChapter.Path != null) _isSelected = CurrentChapterPath == EpubChapter.Path;

        if (_isSelected && _lastSelectedChapterPath != EpubChapter.Path)
        {
            _shouldScroll = true;
            _lastSelectedChapterPath = EpubChapter.Path;
        }
    }
}