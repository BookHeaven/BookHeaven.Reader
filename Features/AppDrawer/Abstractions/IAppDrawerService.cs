using AppInfo = BookHeaven.Reader.Features.AppDrawer.Models.AppInfo;

namespace BookHeaven.Reader.Features.AppDrawer.Abstractions;

public interface IAppDrawerService
{
	bool IsLoaded { get; }
	List<AppInfo> Apps { get; set; }
	Action? OnAppsChanged { get; set; }
	Task RefreshInstalledAppsAsync();
	void OpenApp(string packageName);
	void OpenInfo(string packageName);
	void OpenAppShortcut(string packageName, string shortcutId);
	bool CanBeUninstalled(string packageName);
	void UninstallApp(string packageName);
}