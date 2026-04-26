using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.Directives;

public enum PropertyType
{
    Banner,
    Readme
}

[JsonName("PropertyFile")]
[JsonAlias("PropertyFile, Wabbajack.Lib")]
public class PropertyFile : InlineFile
{
    public PropertyType Type;
}