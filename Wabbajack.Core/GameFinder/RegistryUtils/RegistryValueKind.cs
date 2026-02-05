using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <inheritdoc cref="Microsoft.Win32.RegistryValueKind"/>
[PublicAPI]
public enum RegistryValueKind
{
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.String"/>
    String,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.ExpandString"/>
    ExpandString,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.Binary"/>
    Binary,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.DWord"/>
    DWord,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.MultiString"/>
    MultiString,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.QWord"/>
    QWord,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.Unknown"/>
    Unknown,
    /// <inheritdoc cref="Microsoft.Win32.RegistryValueKind.None"/>
    None,
}
