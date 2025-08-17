namespace BookHeaven.Reader.Entities;

public class AppInfo
{
	public string? Name { get; set; }
	public string? PackageName { get; set; }
	public string? IconBase64 { get; set; }

	public DateTime? Date { get; set; }

	public List<AppShortcut> Shortcuts { get; set; } = [];
}