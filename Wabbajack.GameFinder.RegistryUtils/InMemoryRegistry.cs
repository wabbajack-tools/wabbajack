using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

/// <summary>
/// Implementation of <see cref="IRegistry"/> that is entirely in-memory for use in tests.
/// </summary>
[PublicAPI]
public sealed class InMemoryRegistry : IRegistry
{
    private readonly Dictionary<RegistryHive, InMemoryRegistryKey> _baseKeys;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public InMemoryRegistry()
    {
        _baseKeys = Enum
            .GetValues<RegistryHive>()
            .ToDictionary(
                hive => hive,
                hive => new InMemoryRegistryKey(hive, hive.RegistryHiveToString()));
    }

    /// <summary>
    /// Adds a key to the registry.
    /// </summary>
    /// <param name="hive"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public InMemoryRegistryKey AddKey(RegistryHive hive, string key)
    {
        // normalize
        key = key.Replace('/', '\\');

        var keyNames = key.Split('\\', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parent = _baseKeys[hive];

        foreach (var keyName in keyNames)
        {
            var child = parent.AddSubKey(keyName);
            parent = child;
        }

        return parent;
    }

    /// <inheritdoc/>
    public IRegistryKey OpenBaseKey(RegistryHive hive, RegistryView view = RegistryView.Default)
    {
        return _baseKeys[hive];
    }
}

/// <summary>
/// Implementation of <see cref="IRegistryKey"/> that is entirely in-memory for use in tests.
/// </summary>
[PublicAPI]
public sealed class InMemoryRegistryKey : IRegistryKey
{
    private readonly RegistryHive _hive;
    private readonly string _key;
    private readonly string _name;
    private readonly InMemoryRegistryKey _parent;
    private readonly Dictionary<string, InMemoryRegistryKey> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    internal InMemoryRegistryKey(RegistryHive hive, string key)
    {
        _hive = hive;
        _key = key;
        _name = _key;
        _parent = this;
    }

    internal InMemoryRegistryKey(RegistryHive hive, InMemoryRegistryKey parent, string key)
    {
        _hive = hive;
        _parent = parent;
        _key = key;
        _name = $"{parent._name}\\{_key}";
    }

    /// <summary>
    /// Gets the parent key.
    /// </summary>
    /// <returns></returns>
    public InMemoryRegistryKey GetParent() => _parent;

    /// <summary>
    /// Adds a sub-key to the current key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public InMemoryRegistryKey AddSubKey(string key)
    {
        if (_children.TryGetValue(key, out var child)) return child;

        child = new InMemoryRegistryKey(_hive, this, key);
        _children.Add(key, child);

        return child;
    }

    /// <summary>
    /// Adds a value to the key.
    /// </summary>
    /// <param name="valueName"></param>
    /// <param name="value"></param>
    public void AddValue(string valueName, object value)
    {
        if (_values.ContainsKey(valueName)) return;
        _values.Add(valueName, value);
    }

    /// <inheritdoc/>
    public IRegistryKey? OpenSubKey(string name)
    {
        // normalize
        name = name.Replace('/', '\\');

        var names = name.Split('\\', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (_children.TryGetValue(names[0], out var child))
        {
            return names.Length == 1 ? child : child.OpenSubKey(names.Skip(1).Aggregate((a, b) => $"{a}\\{b}"));
        }

        // TODO: this is only a stop-gap measure
        if (string.Equals(_key, "Software", StringComparison.OrdinalIgnoreCase))
        {
            if (_children.TryGetValue("Wow6432Node", out var wowNode))
            {
                return wowNode.OpenSubKey(name);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetSubKeyNames()
    {
        return _children.Keys;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetValueNames()
    {
        return _values.Keys;
    }

    /// <inheritdoc/>
    public RegistryValueKind GetValueKind(string? valueName)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public string GetName() => _name;

    /// <summary>
    /// Gets the last part of the full key.
    /// </summary>
    /// <returns></returns>
    public string GetKeyName() => _key;

    /// <inheritdoc/>
    public object? GetValue(string? valueName)
    {
        if (valueName is null) return _parent.GetValue(_key);

        _values.TryGetValue(valueName, out var value);
        return value;
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
