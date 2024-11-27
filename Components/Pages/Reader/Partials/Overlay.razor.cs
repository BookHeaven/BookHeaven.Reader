using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using BookHeaven.Reader.ViewModels;
using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class Overlay
{
    private OverlayViewModel _overlayViewModel = new();

    [Parameter] public bool ShowOverlay { get; set; }

    [Parameter] public EventCallback<bool> ShowOverlayChanged { get; set; }

    [Parameter] public bool ShowSpine { get; set; }

    [Parameter] public EventCallback<bool> ShowSpineChanged { get; set; }

    [Parameter] public ProfileSettings? ProfileSettings { get; set; } = null!;

    [Parameter] public string? BookTitle { get; set; }

    [Parameter] public string? ChapterTitle { get; set; }
    
    [Parameter] public decimal Progress { get; set; }

    [Parameter] public EventCallback SettingsChanged { get; set; }

    [Parameter] public EventCallback<NavigationButton> GoToChapter { get; set; }

    public void Dispose()
    {
        ProfileSettings!.PropertyChanged -= OnSettingsChanged;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ProfileSettings!.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvokeAsync(StateHasChanged);
        SettingsChanged.InvokeAsync();
    }
}