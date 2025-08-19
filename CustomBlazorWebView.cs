using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.FileProviders;

namespace BookHeaven.Reader
{
	public partial class CustomBlazorWebView : BlazorWebView
	{
		/*
		 * This is so files from the app data directory can be served (covers, books)
		 * Reference: https://stackoverflow.com/questions/72513093/how-to-display-local-image-as-well-as-resources-image-in-net-maui-blazor/75282680#75282680
		 * */
		public override IFileProvider CreateFileProvider(string contentRootDir)
		{
			var lPhysicalFiles = new PhysicalFileProvider(FileSystem.Current.AppDataDirectory);
			return new CompositeFileProvider(lPhysicalFiles, base.CreateFileProvider(contentRootDir));
		}
	}
}
