using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <summary>
/// Represents the Windows Registry. Use either <see cref="WindowsRegistry"/> or <see cref="InMemoryRegistry"/>
/// depending on your needs.
/// </summary>
[PublicAPI]
public interface IRegistry
{
    /// <summary>
    /// Opens the base key of a hive.
    /// </summary>
    /// <param name="hive"></param>
    /// <param name="view"></param>
    /// <returns></returns>
    IRegistryKey OpenBaseKey(RegistryHive hive, RegistryView view = RegistryView.Default);
}
