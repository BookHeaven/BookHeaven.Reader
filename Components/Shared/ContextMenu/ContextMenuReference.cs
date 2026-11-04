using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Shared.ContextMenu;

public sealed class ContextMenuReference
{
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public RenderFragment? Content { get; set; }
}