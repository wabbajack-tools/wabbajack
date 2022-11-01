using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs;

public abstract class Directive
{
    public Hash Hash { get; set; }
    public long Size { get; set; }

    /// <summary>
    ///     location the file will be copied to, relative to the install path.
    /// </summary>
    public RelativePath To { get; set; }

    [JsonIgnore] public virtual bool IsDeterministic => true;
}