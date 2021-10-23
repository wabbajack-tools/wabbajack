using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

[JsonName("InlineFile")]
[JsonAlias("InlineFile, Wabbajack.Lib")]
public class InlineFile : Directive
{
    /// <summary>
    ///     Data that will be written as-is to the destination location;
    /// </summary>
    public RelativePath SourceDataID { get; set; }
}