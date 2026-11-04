namespace BookHeaven.Reader.Features.Reader.Services;

public sealed class ImageViewerService
{
    public Action<string>? OnOpenImage { get; set; }
}