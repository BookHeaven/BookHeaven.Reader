using MyBookHeaven.Reader.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyBookHeaven.Reader.Platforms.Services
{
	public class AppsService : IAppsService
	{
		public List<Entities.AppInfo> GetInstalledApps()
		{
			throw new NotImplementedException();
		}

		public void OpenApp(string packageName)
		{
			throw new NotImplementedException();
		}
	}
}
