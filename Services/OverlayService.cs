namespace BookHeaven.Reader.Services;

public class OverlayService
{
    public Action? OnOverlayChanged { get; set; }
    public bool IsOverlayVisible { get; private set; }
    public bool IsTocVisible { get; private set; }

    public enum OverlayPanel
    {
        None,
        FontSettings,
        PageSettings,
    }
    public OverlayPanel CurrentOverlayPanel { get; set; } = OverlayPanel.None;
    
    public void Initialize()
    {
        IsOverlayVisible = false;
        IsTocVisible = false;
        CurrentOverlayPanel = OverlayPanel.None;
    }
    public void ToggleOverlay()
    {
        IsOverlayVisible = !IsOverlayVisible;
        if (IsOverlayVisible)
        {
            CurrentOverlayPanel = OverlayPanel.None;
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
    public void TogglePanel(OverlayPanel overlayPanel)
    {
        CurrentOverlayPanel = CurrentOverlayPanel == overlayPanel ? OverlayPanel.None : overlayPanel;
    }
}