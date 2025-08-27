using BookHeaven.Reader.Enums;

namespace BookHeaven.Reader.Functions;

public static class ReaderFunctions
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
    
    public static string KelvinToRgb(int kelvin, double opacity)
    {
        kelvin = Math.Clamp(kelvin, 1000, 40000) / 100;

        var r = kelvin <= 66 ? 255 : (int)(329.698727446 * Math.Pow(kelvin - 60, -0.1332047592));
        var g = kelvin <= 66 ? (int)(99.4708025861 * Math.Log(kelvin) - 161.1195681661) : (int)(288.1221695283 * Math.Pow(kelvin - 60, -0.0755148492));
        var b = kelvin >= 66 ? 255 : (kelvin <= 19 ? 0 : (int)(138.5177312231 * Math.Log(kelvin - 10) - 305.0447927307));

        return $"rgba({Math.Clamp(r, 0, 255)}, {Math.Clamp(g, 0, 255)}, {Math.Clamp(b, 0, 255)}, {opacity.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)})";
    }
}