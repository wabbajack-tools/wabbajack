using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <summary>
/// Represents a key in the registry.
/// </summary>
[PublicAPI]
public interface IRegistryKey : IDisposable
{
    /// <summary>
    /// Opens a sub-key.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IRegistryKey? OpenSubKey(string name);

    /// <summary>
    /// Returns the names of all sub-keys.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetSubKeyNames();

    /// <summary>
    /// Returns the names of all values.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetValueNames();

    /// <summary>
    /// Returns the <see cref="RegistryValueKind"/> of a value.
    /// </summary>
    /// <param name="valueName"></param>
    /// <returns></returns>
    RegistryValueKind GetValueKind(string? valueName);

    string GetName();

    /// <summary>
    /// Returns a value.
    /// </summary>
    /// <param name="valueName"></param>
    /// <returns></returns>
    object? GetValue(string? valueName);

    /// <summary>
    /// Tries to get a value.
    /// </summary>
    /// <param name="valueName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetValue(string? valueName, [MaybeNullWhen(false)] out object value)
    {
        value = GetValue(valueName);
        return value is not null;
    }

    /// <summary>
    /// Gets a string.
    /// </summary>
    /// <param name="valueName"></param>
    /// <returns></returns>
    public string? GetString(string? valueName)
    {
        var value = GetValue(valueName);
        return (string?)value;
    }

    /// <summary>
    /// Tries to get a string.
    /// </summary>
    /// <param name="valueName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetString(string? valueName, [MaybeNullWhen(false)] out string value)
    {
        value = null;

        var obj = GetValue(valueName);
        if (obj is not string s) return false;

        value = s;
        return true;
    }
}
