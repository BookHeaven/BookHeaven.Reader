using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Components.Pages;

public partial class Apps : IDisposable
{
    private List<AppInfo> FilteredApps =>
        _sortBy switch
        {
            SortBy.Added => AppsService.Apps.OrderBy(x => x.FirstInstallTime).ToList(),
            SortBy.Name => AppsService.Apps.OrderBy(x => x.Name).ToList(),
            _ => AppsService.Apps
        };
    
    private enum SortBy
    {
        Added,
        Name
    }

    private SortBy _sortBy = SortBy.Added;

    protected override void OnInitialized()
    {
        AppsService.OnAppsChanged += StateHasChanged;
    }
    
    public void Dispose()
    {
        AppsService.OnAppsChanged -= StateHasChanged;
        GC.SuppressFinalize(this);
    }
}