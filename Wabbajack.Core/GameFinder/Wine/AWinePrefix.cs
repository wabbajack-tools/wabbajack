using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Wabbajack.GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.Wine;

/// <summary>
/// Abstract class for wine prefixes.
/// </summary>
[PublicAPI]
public abstract record AWinePrefix
{
    /// <summary>
    /// Absolute path to the Wine prefix directory.
    /// </summary>
    public required AbsolutePath ConfigurationDirectory { get; init; }

    /// <summary>
    /// Returns the absolute path to the virtual drive directory of the prefix.
    /// </summary>
    /// <returns></returns>
    public AbsolutePath GetVirtualDrivePath()
    {
        return ConfigurationDirectory.Combine("drive_c");
    }

    /// <summary>
    /// Returns the absolute path to the <c>system.reg</c> file of the prefix.
    /// </summary>
    /// <returns></returns>
    public AbsolutePath GetSystemRegistryFile()
    {
        return ConfigurationDirectory.Combine("system.reg");
    }

    /// <summary>
    /// Returns the absolute path to the <c>user.reg</c> file of the prefix.
    /// </summary>
    /// <returns></returns>
    public AbsolutePath GetUserRegistryFile()
    {
        return ConfigurationDirectory.Combine("user.reg");
    }

    /// <summary>
    /// Returns the username for this wine prefix.
    /// </summary>
    /// <returns></returns>
    protected virtual string GetUserName()
    {
        var user = Environment.GetEnvironmentVariable("USER", EnvironmentVariableTarget.Process);
        if (user is null) throw new PlatformNotSupportedException();
        return user;
    }

    /// <summary>
    /// Creates an overlay <see cref="IFileSystem"/> with path
    /// mappings into the wine prefix.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public IFileSystem CreateOverlayFileSystem(IFileSystem fileSystem)
    {
        var rootDirectory = GetVirtualDrivePath();

        var newHomeDirectory = rootDirectory
            .Combine("users")
            .Combine(GetUserName());

        var (pathMappings, knownPathMappings) = BaseFileSystem.CreateWinePathMappings(
            fileSystem,
            rootDirectory,
            newHomeDirectory);

        return fileSystem.CreateOverlayFileSystem(pathMappings, knownPathMappings, convertCrossPlatformPaths: true);
    }

    /// <summary>
    /// Creates a new <see cref="IRegistry"/> implementation,
    /// based on the registry files in the configuration
    /// directory.
    /// </summary>
    /// <returns></returns>
    public IRegistry CreateRegistry(IFileSystem fileSystem)
    {
        var registry = new InMemoryRegistry();

        var registryFile = GetSystemRegistryFile();
        if (!fileSystem.FileExists(registryFile)) return registry;

        using var stream = fileSystem.ReadFile(registryFile);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        InMemoryRegistryKey? currentKey = null;

        while (true)
        {
            var line = reader.ReadLine();
            if (line is null) break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("WINE REGISTRY VERSION", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith(";;", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('#'))
                continue;

            if (line.StartsWith('['))
            {
                var squareBracketIndex = line.IndexOf(']', StringComparison.OrdinalIgnoreCase);
                var keyName = line.Substring(1, squareBracketIndex - 1);

                currentKey = registry.AddKey(RegistryHive.LocalMachine, keyName);
                continue;
            }

            if (line.StartsWith("@=", StringComparison.OrdinalIgnoreCase))
            {
                if (currentKey is null) throw new UnreachableException();
                if (line[2] == '"')
                {
                    var endIndex = line.LastIndexOf('"');
                    var value = line.Substring(3, endIndex - 3);

                    currentKey.GetParent().AddValue(currentKey.GetKeyName(), value);
                    continue;
                }

                // TODO: handle other cases
            }

            if (line.StartsWith('"'))
            {
                if (currentKey is null) throw new UnreachableException();
                var splitIndex = line.IndexOf("\"=\"", StringComparison.OrdinalIgnoreCase);

                // TODO: handle more cases
                if (splitIndex == -1) continue;

                var valueName = line.Substring(1, splitIndex - 1);
                var value = line.Substring(splitIndex + 3, line.Length - splitIndex - 4);
                currentKey.AddValue(valueName, value);
                continue;
            }

            // TODO: handle other cases
        }

        return registry;
    }
}
