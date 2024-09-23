using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Functions;

public static class ReaderNavigation
{
    public static List<NavigationButton> GetLayoutForType(NavigationLayoutType type)
    {
        List<NavigationButton> buttons = [];
        switch (type)
        {
            case NavigationLayoutType.Type1:
                buttons = [NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next];
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next]);
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next]);
                break;
            case NavigationLayoutType.Type2:
                buttons = [NavigationButton.Next, NavigationButton.Overlay, NavigationButton.Next];
                buttons.AddRange([NavigationButton.Next, NavigationButton.Overlay, NavigationButton.Next]);
                buttons.AddRange([NavigationButton.Next, NavigationButton.Overlay, NavigationButton.Next]);
                break;
            case NavigationLayoutType.Type3:
                buttons = [NavigationButton.Overlay, NavigationButton.Overlay, NavigationButton.Overlay];
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next]);
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next]);
                break;
            case NavigationLayoutType.Type4:
                buttons = [NavigationButton.Previous, NavigationButton.Previous, NavigationButton.Next];
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Overlay, NavigationButton.Next]);
                buttons.AddRange([NavigationButton.Previous, NavigationButton.Next, NavigationButton.Next]);
                break;
        }

        return buttons;
    }
}