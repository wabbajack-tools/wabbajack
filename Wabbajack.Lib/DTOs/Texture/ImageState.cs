namespace Wabbajack.DTOs.Texture;

public class ImageState
{
    public int Width { get; set; }
    public int Height { get; set; }
    public DXGI_FORMAT Format { get; set; }
    public PHash PerceptualHash { get; set; }

    public int MipLevels { get; set; }

    public override string ToString()
    {
        return $"ImageState<{Width}, {Height}, {Format}, {MipLevels} levels>";
    }
}