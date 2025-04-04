namespace BookHeaven.Reader.Entities;

public class AppInfo
{
	public string? Name { get; set; }
	public string? PackageName { get; set; }
	public string? IconBase64 { get; set; }

	public DateTime? FirstInstallTime { get; set; }
	public DateTime? LastOpened { get; set; }
}