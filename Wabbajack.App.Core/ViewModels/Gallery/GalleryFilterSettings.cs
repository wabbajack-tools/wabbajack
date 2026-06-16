namespace Wabbajack;

public class GalleryFilterSettings
{
    public string GameType { get; set; }
    public bool IncludeNSFW { get; set; }
    public bool IncludeUnofficial { get; set; }
    public bool OnlyInstalled { get; set; }
    public string Search { get; set; }
    public bool ExcludeMods { get; set; }
}
