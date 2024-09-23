using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Components.Pages;

public partial class Apps
{
    private List<AppInfo> _apps = new();
    private List<AppInfo> _filteredApps = new();

    private Sorting _sortBy = Sorting.Added;

    protected override void OnInitialized()
    {
        _apps = AppsService.GetInstalledApps();
        Sort(_sortBy);
    }

    private void Sort(Sorting sort)
    {
        _sortBy = sort;
        switch (_sortBy)
        {
            case Sorting.Added:
                _filteredApps = _apps.OrderBy(x => x.FirstInstallTime).ToList();
                break;
            case Sorting.Name:
                _filteredApps = _apps.OrderBy(x => x.Name).ToList();
                break;
        }
    }

    private enum Sorting
    {
        Added,
        Name
    }
}