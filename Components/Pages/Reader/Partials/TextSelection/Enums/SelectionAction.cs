namespace BookHeaven.Reader.Components.Pages.Reader.Partials.TextSelection.Enums;

[Flags]
internal enum SelectionAction
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