using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths.Utilities;
using Reloaded.Memory.Extensions;

[assembly: InternalsVisibleTo("Wabbajack.GameFinder.Paths.Tests")]
namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// A path that represents a full path to a file or directory.
/// </summary>
[PublicAPI]
public readonly partial struct AbsolutePath : IEquatable<AbsolutePath>, IPath<AbsolutePath>
{
    /// <summary>
    /// The directory component of the path.
    /// </summary>
    /// <remarks>
    /// This string is never empty and might end with a directory separator.
    /// This is only guaranteed for root directories, every other directory
    /// shall not have trailing directory separators.
    /// </remarks>
    /// <example><c>/foo/bar</c></example>
    public readonly string Directory;

    /// <summary>
    /// The characters after the last directory separator.
    /// </summary>
    /// <remarks>
    /// This string can be empty if the entire path is just a root directory.
    /// </remarks>
    /// <example><c>README.md</c></example>
    public readonly string FileName;

    /// <inheritdoc />
    RelativePath IPath.FileName => Name;

    /// <summary>
    /// The <see cref="IFileSystem"/> implementation used by the IO methods.
    /// </summary>
    public IFileSystem FileSystem { get; init; }

    /// <summary>
    /// Returns a new path, identical to this one, but with the filesystem replaced with the given filesystem
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public AbsolutePath WithFileSystem(IFileSystem fileSystem)
    {
        return new AbsolutePath(Directory, FileName, fileSystem);
    }

    /// <summary>
    /// Returns the FileName as a <see cref="RelativePath"/>.
    /// </summary>
    /// <remarks>
    /// If this is a root directory, returns <see cref="RelativePath.Empty"/>.
    /// </remarks>
    public RelativePath Name =>  string.IsNullOrEmpty(FileName) ? RelativePath.Empty : new RelativePath(FileName);

    /// <inheritdoc />
    public Extension Extension => string.IsNullOrEmpty(FileName) ? Extension.None : Extension.FromPath(FileName);


    /// <summary>
    /// Gets the parent directory, i.e. navigates one folder up.
    /// </summary>
    public AbsolutePath Parent
    {
        get
        {
            var directory = PathHelpers.GetDirectoryName(Directory);
            var fileName = PathHelpers.GetFileName(Directory);
            return new AbsolutePath(directory.ToString(), fileName.ToString(), FileSystem);
        }
    }

    /// <summary>
    /// Returns the root folder of this path.
    /// </summary>
    public AbsolutePath GetRootComponent => GetRootDirectory();

    /// <inheritdoc/>
    public IEnumerable<RelativePath> Parts =>
        GetNonRootPart().Parts;

    /// <inheritdoc/>
    public IEnumerable<AbsolutePath> GetAllParents()
    {
        var currentPath = this;
        var root = GetRootDirectory();

        while (currentPath != root)
        {
            yield return currentPath;
            currentPath = currentPath.Parent;
        }
        yield return root;
    }

    /// <summary>
    /// Returns the non-root part of this path.
    /// </summary>
    public RelativePath GetNonRootPart()
    {
        return RelativeTo(GetRootDirectory());
    }

    /// <inheritdoc/>
    public bool IsRooted => true;

    private AbsolutePath(string directory, string fileName, IFileSystem fileSystem)
    {
        PathHelpers.DebugAssertIsSanitized(directory);
        PathHelpers.DebugAssertIsSanitized(fileName);
        PathHelpers.AssertIsRooted(directory, shouldBeRooted: true);

        Directory = directory;
        FileName = fileName;
        FileSystem = fileSystem;
    }

    /// <summary>
    /// Creates a new <see cref="AbsolutePath"/> from a sanitized full path.
    /// </summary>
    /// <seealso cref="FromUnsanitizedFullPath"/>
    internal static AbsolutePath FromSanitizedFullPath(ReadOnlySpan<char> fullPath, IFileSystem fileSystem)
    {
        var directory = PathHelpers.GetDirectoryName(fullPath);
        var fileName = PathHelpers.GetFileName(fullPath);
        return new AbsolutePath(directory.ToString(), fileName.ToString(), fileSystem);
    }

    /// <summary>
    /// Creates a new <see cref="AbsolutePath"/> from an unsanitized full path.
    /// </summary>
    /// <seealso cref="FromSanitizedFullPath"/>
    /// <seealso cref="FromUnsanitizedDirectoryAndFileName"/>
    internal static AbsolutePath FromUnsanitizedFullPath(string fullPath, IFileSystem fileSystem)
    {
        return fileSystem.FromUnsanitizedFullPath(fullPath);
    }

    /// <summary>
    /// Creates a new <see cref="AbsolutePath"/> from an unsanitized directory and file name.
    /// </summary>
    /// <seealso cref="FromUnsanitizedFullPath"/>
    internal static AbsolutePath FromUnsanitizedDirectoryAndFileName(
        string directory,
        string fileName,
        IFileSystem fileSystem)
    {
        var sanitizedDirectory = PathHelpers.Sanitize(directory);
        var sanitizedFileName = PathHelpers.Sanitize(fileName);
        var fullPath = PathHelpers.JoinParts(sanitizedDirectory, sanitizedFileName);
        return FromSanitizedFullPath(fullPath, fileSystem);
    }

    /// <summary>
    /// Returns the full path with directory separators matching the passed OS.
    /// </summary>
    public string ToNativeSeparators(IOSInformation os)
    {
        return PathHelpers.ToNativeSeparators(GetFullPath(), os);
    }

    /// <summary>
    /// Returns the file name of the specified path string without the extension.
    /// </summary>
    public string GetFileNameWithoutExtension()
    {
        if (FileName.Length == 0) return string.Empty;
        var span = FileName.AsSpan();

        var length = span.LastIndexOf('.');
        return length >= 0 ? span.SliceFast(0, length).ToString() : span.ToString();
    }

    /// <summary>
    /// Creates a new <see cref="AbsolutePath"/> from the current one, appending the provided
    /// extension to the file name.
    /// </summary>
    /// <remarks>
    /// Do not use this method if you want to change the extension. Use <see cref="ReplaceExtension"/>
    /// instead. This method literally just does <see cref="FileName"/> + <paramref name="ext"/>.
    /// </remarks>
    /// <param name="ext">The extension to append.</param>
    public AbsolutePath AppendExtension(Extension ext)
    {
        return new AbsolutePath(Directory, FileName + ext, FileSystem);
    }

    /// <summary>
    /// Creates a new <see cref="AbsolutePath"/> from the current one, replacing
    /// the existing extension with a new one.
    /// </summary>
    /// <remarks>
    /// This method will behave the same as <see cref="AppendExtension"/>, if the
    /// current <see cref="FileName"/> doesn't have an extension.
    /// </remarks>
    /// <param name="ext">The extension to replace.</param>
    public AbsolutePath ReplaceExtension(Extension ext)
    {
        return new AbsolutePath(Directory, PathHelpers.ReplaceExtension(FileName, ext.ToString()), FileSystem);
    }

    /// <summary>
    /// Returns the full path of the combined string.
    /// </summary>
    /// <returns>The full combined path.</returns>
    public string GetFullPath() => PathHelpers.JoinParts(Directory, FileName);

    /// <summary>
    /// Copies the full path into <paramref name="buffer"/>.
    /// </summary>
    public void GetFullPath(Span<char> buffer)
    {
        PathHelpers.JoinParts(buffer, Directory, FileName);
    }

    /// <summary>
    /// Returns the expected length of the full path.
    /// </summary>
    /// <returns></returns>
    public int GetFullPathLength() => PathHelpers.GetExactJoinedPartLength(Directory, FileName);

    /// <summary>
    /// Obtains the name of the first folder stored in this path.
    /// </summary>
    public AbsolutePath GetRootDirectory()
    {
        var pathRoot = PathHelpers.GetPathRoot(Directory);
        return new AbsolutePath(pathRoot.Span.ToString(), string.Empty, FileSystem);
    }

    /// <summary>
    /// Combines the current path with a relative path.
    /// </summary>
    public AbsolutePath Combine(RelativePath path)
    {
        var res = PathHelpers.JoinParts(GetFullPath(), path.Path);
        return FromSanitizedFullPath(res, FileSystem);
    }
    
    /// <summary>
    /// Combines the current path with a relative path.
    /// </summary>
    public static AbsolutePath operator / (AbsolutePath path, RelativePath relativePath) 
        => path.Combine(relativePath);

    /// <summary/>
    [Obsolete(message: "This will be removed once dependents have updated.", error: true)]
    public AbsolutePath CombineUnchecked(RelativePath path) => Combine(path);

    /// <summary>
    /// Gets a path relative to another absolute path.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="RelativePath.Empty"/> if <see paramref="other"/> is the same as this path.
    /// </remarks>
    /// <param name="other">The path from which the relative path should be made.</param>
    /// <throws><see cref="PathException"/> if the paths are not in the same folder.</throws>
    [SkipLocalsInit]
    public RelativePath RelativeTo(AbsolutePath other)
    {
        var childLength = GetFullPathLength();
        var parentLength = other.GetFullPathLength();

        if (childLength == parentLength && Equals(other)) return RelativePath.Empty;

        var child = childLength <= 512 ? stackalloc char[childLength] : GC.AllocateUninitializedArray<char>(childLength);
        GetFullPath(child);

        var parent = parentLength <= 512 ? stackalloc char[parentLength] : GC.AllocateUninitializedArray<char>(parentLength);
        other.GetFullPath(parent);

        var res = PathHelpers.RelativeTo(child, parent);
        if (!res.IsEmpty) return new RelativePath(res.ToString());

        ThrowHelpers.PathException("Can't create path relative to paths that aren't in the same folder");
        return default;
    }

    /// <inheritdoc />
    [SkipLocalsInit]
    public bool InFolder(AbsolutePath parent)
    {
        var parentLength = parent.GetFullPathLength();
        var parentSpan = parentLength <= 512 ? stackalloc char[parentLength] : GC.AllocateUninitializedArray<char>(parentLength);
        parent.GetFullPath(parentSpan);

        // NOTE(erri120):
        // We need the full path of the "parent", but only the directory name of the "child".
        return PathHelpers.InFolder(Directory, parentSpan);
    }

    /// <inheritdoc />
    public bool StartsWith(AbsolutePath other)
    {
        var fullPath = GetFullPath();
        var prefix = other.GetFullPath();

        if (fullPath.Length < prefix.Length) return false;
        if (fullPath.Length == prefix.Length) return Equals(other);
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // If the other path is a parent of this path, then the next character must be a directory separator.
        return fullPath[prefix.Length] == PathHelpers.DirectorySeparatorChar ||
               // unless the prefix is a root directory
               PathHelpers.IsRootDirectory(prefix);
    }

    /// <inheritdoc />
    public bool EndsWith(RelativePath other)
    {
        return GetNonRootPart().EndsWith(other);
    }

    /// <summary/>
    public static bool operator ==(AbsolutePath lhs, AbsolutePath rhs) => lhs.Equals(rhs);

    /// <summary/>
    public static bool operator !=(AbsolutePath lhs, AbsolutePath rhs) => !(lhs == rhs);

    /// <inheritdoc />
    public override string ToString()
    {
        return this == default ? "<default>" : GetFullPath();
    }

    #region Equals & GetHashCode

    /// <inheritdoc />
    public bool Equals(AbsolutePath other)
    {
        // Do not reorder, FileName is statistically more likely to mismatch than Directory - (Sewer)
        return string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Directory, other.Directory, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AbsolutePath other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var a = PathHelpers.PathHashCode(Directory);
        var b = PathHelpers.PathHashCode(FileName);
        return HashCode.Combine(a, b);
    }

    #endregion
}
