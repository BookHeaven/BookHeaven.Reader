using System;
using System.Collections.Generic;
using Microsoft.Win32;
using BookHeaven.Reader.Interfaces;
using System.Drawing;
using System.IO;
using AppInfo = BookHeaven.Reader.Entities.AppInfo;

namespace BookHeaven.Reader.Services;

public class AppsService : IAppsService
{
	public List<AppInfo> Apps { get; set; } = [];
	public Action? OnAppsChanged { get; set; }
	
    public void OpenApp(string packageName)
    {
    }

    public void OpenInfo(string packageName)
    {
    }
    
    public void OpenAppShortcut(string packageName, string shortcutId)
	{
	}

    public bool CanBeUninstalled(string packageName)
    {
    	return false;
    }

    public void UninstallApp(string packageName)
    {
    }

    public async Task RefreshInstalledAppsAsync()
    {
        await Task.Run(() =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key == null) return;
            // Get the names of all subkeys (which represent installed programs)
            var subkeyNames = key.GetSubKeyNames();

            Apps.Clear();
            // Iterate through each subkey and retrieve program information
            foreach (var subkeyName in subkeyNames)
            {
			    using var subkey = key.OpenSubKey(subkeyName);
			    // Retrieve the program name and display it
			    var displayName = subkey?.GetValue("DisplayName") as string;
			    if (string.IsNullOrEmpty(displayName)) continue;
			    Console.WriteLine(displayName);

			    // Retrieve the display icon path
			    var displayIconPath = subkey?.GetValue("DisplayIcon") as string;
			    if (string.IsNullOrEmpty(displayIconPath) || !File.Exists(displayIconPath)) continue;
			    // Load the icon from the file
			    var icon = Icon.ExtractAssociatedIcon(displayIconPath);
			    if (icon != null)
			    {
				    Apps.Add(new AppInfo
				    {
					    Name = displayName,
					    IconBase64 = ConvertBitmapToBase64(icon)
				    });
			    }
		    }
        });
        OnAppsChanged?.Invoke();
    }

    private string ConvertBitmapToBase64(Icon icon)
    {
    	byte[] iconBytes;
    	using (var ms = new MemoryStream())
    	{
    		icon.Save(ms);
    		iconBytes = ms.ToArray();
    	}

    	// Convert the byte array to a Base64 string
    	var base64Icon = Convert.ToBase64String(iconBytes);
    	return base64Icon;
    }
}
