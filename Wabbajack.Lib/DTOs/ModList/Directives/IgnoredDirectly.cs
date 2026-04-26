using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.Directives;

[JsonName("IgnoredDirectly")] // Should never make it into a JSON file
public class IgnoredDirectly : Directive
{
    public string Reason = string.Empty;
}