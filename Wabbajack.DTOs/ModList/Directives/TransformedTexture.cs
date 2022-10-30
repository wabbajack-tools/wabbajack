using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Texture;

namespace Wabbajack.DTOs.Directives;

[JsonName("TransformedTexture")]
[JsonAlias("TransformedTexture, Wabbajack.Lib")]
public class TransformedTexture : FromArchive
{
    /// <summary>
    ///     The file to apply to the source file to patch it
    /// </summary>
    public ImageState ImageState { get; set; } = new();
    public override bool IsDeterministic => false;
}