using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Components.Pages;

public partial class Apps
{
    private List<AppInfo> DeviceApps => AppsService.GetInstalledApps();
    private List<AppInfo> FilteredApps =>
        _sortBy switch
        {
            SortBy.Added => DeviceApps.OrderBy(x => x.FirstInstallTime).ToList(),
            SortBy.Name => DeviceApps.OrderBy(x => x.Name).ToList(),
            _ => DeviceApps
        };
    
    private enum SortBy
    {
        Added,
        Name
    }

    private SortBy _sortBy = SortBy.Added;

    
}