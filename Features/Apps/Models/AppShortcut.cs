namespace BookHeaven.Reader.Features.Apps.Models;

public sealed class AppShortcut
{
    public string? Id { get; set; }
    public string? ShortLabel { get; set; }
    public string? LongLabel { get; set; }
    public string? IconBase64 { get; set; }
}