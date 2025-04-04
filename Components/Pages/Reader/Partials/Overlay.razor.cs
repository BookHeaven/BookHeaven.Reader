using System.ComponentModel;
using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Services;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class Overlay
{
    [Inject] OverlayService OverlayService { get; set; } = null!;
    [Parameter] public ProfileSettings? ProfileSettings { get; set; } = null!;
    [Parameter] public string? BookTitle { get; set; }
    [Parameter] public string? ChapterTitle { get; set; }
    [Parameter] public decimal Progress { get; set; }
    [Parameter] public EventCallback SettingsChanged { get; set; }
    [Parameter] public EventCallback<NavigationButton> GoToChapter { get; set; }

    public void Dispose()
    {
        ProfileSettings!.PropertyChanged -= OnSettingsChanged;
        GC.SuppressFinalize(this);
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