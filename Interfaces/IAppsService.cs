using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Interfaces
{
	public interface IAppsService
	{
		List<AppInfo> GetInstalledApps();
		void OpenApp(string packageName);
		void OpenInfo(string packageName);
		bool CanBeUninstalled(string packageName);
		void UninstallApp(string packageName);
	}
}
