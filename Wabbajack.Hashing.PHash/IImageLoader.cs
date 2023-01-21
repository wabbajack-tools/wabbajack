using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shipwreck.Phash;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;

namespace Wabbajack.Hashing.PHash;

public interface IImageLoader
{
    public ValueTask<ImageState> Load(AbsolutePath path);
    public ValueTask<ImageState> Load(Stream stream);
    public static float ComputeDifference(DTOs.Texture.PHash a, DTOs.Texture.PHash b)
    {
        return ImagePhash.GetCrossCorrelation(
            new Digest {Coefficients = a.Data},
            new Digest {Coefficients = b.Data});
    }

    public Task Recompress(AbsolutePath input, int width, int height, DXGI_FORMAT format,
        AbsolutePath output,
        CancellationToken token);

    public Task Recompress(Stream input, int width, int height, DXGI_FORMAT format, Stream output,
        CancellationToken token, bool leaveOpen = false);
}