using System;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Represents version information for a file on disk.
/// </summary>
/// <param name="ProductVersion">Gets the version of the product this file is distributed with.</param>
/// <param name="FileVersion">Gets the file version number.</param>
/// <param name="ProductVersionString">Gets the version of the product this file is distributed with.</param>
/// <param name="FileVersionString">Gets the file version number.</param>
[PublicAPI]
public record struct FileVersionInfo(
    Version ProductVersion,
    Version FileVersion,
    string? ProductVersionString,
    string? FileVersionString)
{
    private static readonly Version Zero = new(0, 0, 0, 0);

    /// <summary>
    /// Returns the first non-zero version.
    /// </summary>
    public Version GetBestVersion()
    {
        return ProductVersion.Equals(Zero)
            ? FileVersion
            : ProductVersion;
    }

    /// <summary>
    /// Returns the first non-null version string.
    /// </summary>
    public string? GetVersionString()
    {
        return ProductVersionString ?? FileVersionString;
    }
}
