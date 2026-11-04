namespace BookHeaven.Reader.Features.Apps.Models;

public sealed class AppInfo
{
	public string? Name { get; set; }
	public string? PackageName { get; set; }
	public string? IconBase64 { get; set; }

	public DateTime? Date { get; set; }

	public List<AppShortcut> Shortcuts { get; set; } = [];
}