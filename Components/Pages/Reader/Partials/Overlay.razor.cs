using System.ComponentModel;
using BookHeaven.Domain.Entities;
using BookHeaven.Reader.Enums;
using BookHeaven.Reader.Services;
using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials;

public partial class Overlay
{
    [Inject] private OverlayService OverlayService { get; set; } = null!;
    [Parameter] public string? BookTitle { get; set; }
    [Parameter] public string? ChapterTitle { get; set; }
    [Parameter] public decimal Progress { get; set; }
    [Parameter] public EventCallback<NavigationButton> GoToChapter { get; set; }
}