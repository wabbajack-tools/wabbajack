using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <inheritdoc cref="Microsoft.Win32.RegistryView"/>
[PublicAPI]
public enum RegistryView
{
    /// <inheritdoc cref="Microsoft.Win32.RegistryView.Default"/>
    Default,
    /// <inheritdoc cref="Microsoft.Win32.RegistryView.Registry64"/>
    Registry64,
    /// <inheritdoc cref="Microsoft.Win32.RegistryView.Registry32"/>
    Registry32,
}
