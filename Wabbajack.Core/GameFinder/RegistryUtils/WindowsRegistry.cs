using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

[SupportedOSPlatform("windows")]
internal static class InteropHelpers
{
    public static Microsoft.Win32.RegistryHive Convert(RegistryHive hive)
    {
        return hive switch
        {
            RegistryHive.ClassesRoot => Microsoft.Win32.RegistryHive.ClassesRoot,
            RegistryHive.CurrentUser => Microsoft.Win32.RegistryHive.CurrentUser,
            RegistryHive.LocalMachine => Microsoft.Win32.RegistryHive.LocalMachine,
            RegistryHive.Users => Microsoft.Win32.RegistryHive.Users,
            RegistryHive.PerformanceData => Microsoft.Win32.RegistryHive.PerformanceData,
            RegistryHive.CurrentConfig => Microsoft.Win32.RegistryHive.CurrentConfig,
            _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, null)
        };
    }

    public static Microsoft.Win32.RegistryView Convert(RegistryView view)
    {
        return view switch
        {
            RegistryView.Default => Microsoft.Win32.RegistryView.Default,
            RegistryView.Registry64 => Microsoft.Win32.RegistryView.Registry64,
            RegistryView.Registry32 => Microsoft.Win32.RegistryView.Registry32,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, null)
        };
    }

    public static RegistryValueKind Convert(Microsoft.Win32.RegistryValueKind valueKind)
    {
        return valueKind switch
        {
            Microsoft.Win32.RegistryValueKind.None => RegistryValueKind.None,
            Microsoft.Win32.RegistryValueKind.Unknown => RegistryValueKind.Unknown,
            Microsoft.Win32.RegistryValueKind.String => RegistryValueKind.String,
            Microsoft.Win32.RegistryValueKind.ExpandString => RegistryValueKind.ExpandString,
            Microsoft.Win32.RegistryValueKind.Binary => RegistryValueKind.Binary,
            Microsoft.Win32.RegistryValueKind.DWord => RegistryValueKind.DWord,
            Microsoft.Win32.RegistryValueKind.MultiString => RegistryValueKind.MultiString,
            Microsoft.Win32.RegistryValueKind.QWord => RegistryValueKind.QWord,
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, null)
        };
    }
}

/// <summary>
/// Implementation of <see cref="IRegistry"/> that uses the Windows registry.
/// </summary>
[PublicAPI]
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistry : IRegistry
{
    /// <summary>
    /// Shared instance of <see cref="IRegistry"/> for Windows.
    /// </summary>
    public static readonly IRegistry Shared = new WindowsRegistry();

    /// <inheritdoc/>
    public IRegistryKey OpenBaseKey(RegistryHive hive, RegistryView view = RegistryView.Default)
    {
        return new WindowsRegistryKey(hive, view);
    }
}

/// <summary>
/// Implementation of <see cref="IRegistryKey"/> that uses <see cref="Microsoft.Win32.RegistryKey"/>.
/// </summary>
[PublicAPI]
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryKey : IRegistryKey
{
    private readonly Microsoft.Win32.RegistryKey _registryKey;

    internal WindowsRegistryKey(RegistryHive hive, RegistryView view)
    {
        _registryKey = Microsoft.Win32.RegistryKey.OpenBaseKey(InteropHelpers.Convert(hive), InteropHelpers.Convert(view));
    }

    private WindowsRegistryKey(Microsoft.Win32.RegistryKey key)
    {
        _registryKey = key;
    }

    /// <inheritdoc/>
    public IRegistryKey? OpenSubKey(string name)
    {
        var key = _registryKey.OpenSubKey(name);
        return key is null ? null : new WindowsRegistryKey(key);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetSubKeyNames()
    {
        return _registryKey.GetSubKeyNames();
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetValueNames()
    {
        return _registryKey.GetValueNames();
    }

    /// <inheritdoc/>
    public RegistryValueKind GetValueKind(string? valueName)
    {
        return InteropHelpers.Convert(_registryKey.GetValueKind(valueName));
    }

    /// <inheritdoc/>
    public string GetName()
    {
        return _registryKey.Name;
    }

    /// <inheritdoc/>
    public object? GetValue(string? valueName)
    {
        return _registryKey.GetValue(valueName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _registryKey.Dispose();
    }
}
