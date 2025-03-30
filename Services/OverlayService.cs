namespace BookHeaven.Reader.Services;

public class OverlayService
{
    public Action? OnOverlayChanged { get; set; } = null!;
    public bool IsOverlayVisible { get; private set; }
    public bool IsTocVisible { get; private set; }

    public enum Panel
    {
        None,
        FontSettings,
        PageSettings,
    }
    public Panel CurrentPanel { get; set; } = Panel.None;
    
    public void Init()
    {
        IsOverlayVisible = false;
        IsTocVisible = false;
        CurrentPanel = Panel.None;
    }
    public void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
        if (IsOverlayVisible)
        {
            CurrentPanel = Panel.None;
            IsTocVisible = false;
        }  
        OnOverlayChanged?.Invoke();
    }
    
    public void ToggleToc()
    {
        IsTocVisible = !IsTocVisible;
        if (IsTocVisible)
            IsOverlayVisible = false;
        OnOverlayChanged?.Invoke();
    }
    public void TogglePanel(Panel panel)
    {
        CurrentPanel = CurrentPanel == panel ? Panel.None : panel;
    }
    
    
}