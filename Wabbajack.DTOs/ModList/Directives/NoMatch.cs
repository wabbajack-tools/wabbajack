using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.Directives;

[JsonName("NoMatch")] // Should never make it into a JSON file
public class NoMatch : IgnoredDirectly
{
}