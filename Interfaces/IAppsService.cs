using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Interfaces;

public interface IAppsService
{
	
	List<AppInfo> Apps { get; set; }
	Action? OnAppsChanged { get; set; }
	void RefreshInstalledApps();
	void OpenApp(string packageName);
	void OpenInfo(string packageName);
	void OpenAppShortcut(string packageName, string shortcutId);
	bool CanBeUninstalled(string packageName);
	void UninstallApp(string packageName);
}