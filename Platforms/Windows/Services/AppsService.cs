using Microsoft.Win32;
using BookHeaven.Reader.Interfaces;
using System.Drawing;
using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Services
{

	public class AppsService : IAppsService
	{
		public void OpenApp(string packageName)
		{
		}

		public void OpenInfo(string packageName)
		{
		}

		public bool CanBeUninstalled(string packageName)
		{
			return false;
		}

		public void UninstallApp(string packageName)
		{
		}

		public List<AppInfo> GetInstalledApps()
		{
			List<AppInfo> apps = [];
			using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
			{
				if (key != null)
				{
					// Get the names of all subkeys (which represent installed programs)
					string[] subkeyNames = key.GetSubKeyNames();

					// Iterate through each subkey and retrieve program information
					foreach (string subkeyName in subkeyNames)
					{
						using (RegistryKey? subkey = key.OpenSubKey(subkeyName))
						{
							// Retrieve the program name and display it
							string? displayName = subkey.GetValue("DisplayName") as string;
							if (!string.IsNullOrEmpty(displayName))
							{
								Console.WriteLine(displayName);

								// Retrieve the display icon path
								string? displayIconPath = subkey.GetValue("DisplayIcon") as string;
								if (!string.IsNullOrEmpty(displayIconPath) && File.Exists(displayIconPath))
								{
									// Load the icon from the file
									Icon? icon = Icon.ExtractAssociatedIcon(displayIconPath);
									if (icon != null)
									{
										apps.Add(new AppInfo
										{
											Name = displayName,
											IconBase64 = ConvertBitmapToBase64(icon)
										});
									}
								}
							}
						}
					}
				}
			}
			return apps;
		}

		private string ConvertBitmapToBase64(Icon icon)
		{
			byte[] iconBytes;
			using (MemoryStream ms = new MemoryStream())
			{
				icon.Save(ms);
				iconBytes = ms.ToArray();
			}

			// Convert the byte array to a Base64 string
			string base64Icon = Convert.ToBase64String(iconBytes);
			return base64Icon;
		}
	}
}

