using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.Wine.Bottles;

/// <summary>
/// Represents a Wineprefix managed by Bottles.
/// </summary>
[PublicAPI]
public record BottlesWinePrefix : AWinePrefix
{
    /// <summary>
    /// Returns the absolute path to <c>bottle.yml</c>.
    /// </summary>
    /// <returns></returns>
    public AbsolutePath GetBottlesConfigFile()
    {
        return ConfigurationDirectory.Combine("bottle.yml");
    }
}
