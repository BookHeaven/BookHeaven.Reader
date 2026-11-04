using Microsoft.AspNetCore.Components;

namespace BookHeaven.Reader.Components.Shared.ContextMenu;

public interface IContextMenuService
{
    event Func<ContextMenuReference, Task>? OnShow;
    event Action? OnClose;
    Task ShowAsync<TComponent>(double x, double y, ContextMenuParameters? parameters = null) where TComponent : IComponent;
    void CloseMenu();
}

public sealed class ContextMenuService : IContextMenuService
{
    public event Func<ContextMenuReference, Task>? OnShow;
    public event Action? OnClose;
    
    public async Task ShowAsync<TComponent>(double x, double y, ContextMenuParameters? parameters = null) where TComponent : IComponent
    {
        var component = new RenderFragment(builder =>
        {
            builder.OpenComponent<TComponent>(0);
            if (parameters is not null)
            {
                foreach (var parameter in parameters.Index())
                {
                    builder.AddComponentParameter(parameter.Index, parameter.Item.Key, parameter.Item.Value);
                }
            }
            builder.CloseComponent();
        });
        
        var model = new ContextMenuReference
        {
            AnchorX = x,
            AnchorY = y,
            Content = component
        };
        
        if (OnShow is not null)
        {
            await OnShow(model);
        }
    }

    public void CloseMenu()
    {
        OnClose?.Invoke();
    }
}