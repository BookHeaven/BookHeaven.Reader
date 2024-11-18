using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Components.Pages;

public partial class Apps
{
    private List<AppInfo> _apps = new();
    private List<AppInfo> _filteredApps = new();
    
    private enum SortBy
    {
        Added,
        Name
    }

    private SortBy _sortBy = SortBy.Added;

    protected override void OnInitialized()
    {
        _apps = AppsService.GetInstalledApps();
        _sortBy = SortBy.Added;
        Sort();
    }

    private void Sort()
    {
        _filteredApps = _sortBy switch
        {
            SortBy.Added => _apps.OrderBy(x => x.FirstInstallTime).ToList(),
            SortBy.Name => _apps.OrderBy(x => x.Name).ToList(),
            _ => _filteredApps
        };
    }

    
}