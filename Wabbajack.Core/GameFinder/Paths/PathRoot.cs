using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Represents the root part of a path.
/// </summary>
[PublicAPI]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public readonly ref struct PathRoot
{
    /// <summary>
    /// The root part.
    /// </summary>
    public readonly ReadOnlySpan<char> Span;

    /// <summary>
    /// The type.
    /// </summary>
    public readonly PathRootType RootType;

    /// <summary>
    /// Length of the root part.
    /// </summary>
    public int Length => Span.Length;

    /// <summary>
    /// Constructor.
    /// </summary>
    public PathRoot(ReadOnlySpan<char> span, PathRootType rootType)
    {
        Span = span;
        RootType = rootType;
    }

    /// <summary>
    /// <see cref="PathRootType.DOS"/> root parts always have this length.
    /// </summary>
    internal const int DOSRootLength = 3;

    /// <summary>
    /// Minimum length of <see cref="PathRootType.UNC"/> root parts: <c>//A/</c>
    /// </summary>
    internal const int MinUNCRootLength = 4;

    /// <summary>
    /// Length of the DOS device prefix, either `//./` or `//?/`
    /// </summary>
    internal const int DOSDevicePrefixLength = 4;

    /// <summary>
    /// Minimum length of <see cref="PathRootType.DOSDeviceDrive"/> root parts: <c>//./C:/</c>
    /// </summary>
    internal const int DOSDeviceDriveRootLength = 7;

    /// <summary>
    /// Length of <see cref="PathRootType.DOSDeviceVolume"/> root parts: <c>//./Volume{b75e2c83-0000-0000-0000-602f00000000}/</c>
    /// </summary>
    internal const int DOSDeviceVolumeRootLength = 49;

    /// <summary>
    /// Prefix for <see cref="PathRootType.DOSDeviceVolume"/>.
    /// </summary>
    internal const string DOSDeviceVolumePrefix = "Volume{";

    /// <summary>
    /// Volume separator character on Windows.
    /// </summary>
    /// <remarks>
    /// This character is used to separate the drive character of a volume, from the rest
    /// of the path. The path "C:/" has the drive character 'C', the volume separator character
    /// ':' and finally the root directory name '/'.
    /// </remarks>
    public const char WindowsVolumeSeparatorChar = ':';
}

/// <summary>
/// Path root types.
/// </summary>
/// <remarks>
/// For Windows paths see https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats
/// </remarks>
[PublicAPI]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum PathRootType
{
    /// <summary>
    /// None, the path isn't rooted.
    /// </summary>
    None = 0,

    /// <summary>
    /// A Unix-style root.
    /// </summary>
    /// <example><c>/</c></example>
    Unix = 1,

    /// <summary>
    /// A DOS root.
    /// </summary>
    /// <example><c>C:/</c></example>
    DOS = 2,

    /// <summary>
    /// A UNC root.
    /// </summary>
    /// <example><c>//Server/</c></example>
    UNC = 3,

    /// <summary>
    /// A DOS device path with a drive letter.
    /// </summary>
    /// <example><c>//./C:/</c></example>
    /// <example><c>//?/C:/</c></example>
    DOSDeviceDrive = 4,

    /// <summary>
    /// A DOS device path with a volume GUID.
    /// </summary>
    /// <example><c>//./Volume{b75e2c83-0000-0000-0000-602f00000000}/</c></example>
    /// <example><c>//?/Volume{b75e2c83-0000-0000-0000-602f00000000}/</c></example>
    DOSDeviceVolume = 5,
}

/// <summary>
/// Extension methods for <see cref="PathRootType"/>.
/// </summary>
[PublicAPI]
public static class PathRootTypeExtensions
{
    /// <summary>
    /// Whether the root type is a Unix-style root.
    /// </summary>
    public static bool IsUnixRoot(this PathRootType rootType) => rootType == PathRootType.Unix;

    /// <summary>
    /// Whether the root type is a Windows-style root.
    /// </summary>
    public static bool IsWindowsRoot(this PathRootType rootType) => rootType switch
    {
        PathRootType.DOS or PathRootType.UNC or PathRootType.DOSDeviceDrive or PathRootType.DOSDeviceVolume => true,
        _ => false
    };
}
