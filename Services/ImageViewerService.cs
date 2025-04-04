namespace BookHeaven.Reader.Services;

public class ImageViewerService
{
    public Action<string> OnOpenImage { get; set; } = null!;
}