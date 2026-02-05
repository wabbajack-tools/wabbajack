using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <inheritdoc cref="Microsoft.Win32.RegistryHive"/>
[PublicAPI]
public enum RegistryHive
{
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.ClassesRoot"/>
    ClassesRoot,
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.CurrentUser"/>
    CurrentUser,
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.LocalMachine"/>
    LocalMachine,
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.Users"/>
    Users,
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.PerformanceData"/>
    PerformanceData,
    /// <inheritdoc cref="Microsoft.Win32.RegistryHive.CurrentConfig"/>
    CurrentConfig,
}
