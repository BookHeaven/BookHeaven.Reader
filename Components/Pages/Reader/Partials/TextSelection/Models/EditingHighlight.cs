using BookHeaven.Reader.Components.Pages.Reader.Partials.TextSelection.Enums;

namespace BookHeaven.Reader.Components.Pages.Reader.Partials.TextSelection.Models;

internal sealed record EditingHighlight(BookAnnotation Annotation, EditingHighlightState State);
