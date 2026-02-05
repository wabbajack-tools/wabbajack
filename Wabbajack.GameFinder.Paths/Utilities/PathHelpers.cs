using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Reloaded.Memory.Extensions;

namespace Wabbajack.GameFinder.Paths.Utilities;

/// <summary>
/// Helper methods for dealing with paths.
/// </summary>
[PublicAPI]
public static class PathHelpers
{
    [ExcludeFromCodeCoverage(Justification = "Impossible to test.")]
    static PathHelpers()
    {
        // NOTE: The forward slash '/' is supported on BOTH Windows and Unix-based systems.
        // On Windows: Path.DirectorySeparatorChar = '\' and Path.AltDirectorySeparatorChar = '/'
        // On Linux: Path.DirectorySeparatorChar = '/' and Path.AltDirectorySeparatorChar = '/'
        // As such, we can use the forward slash for all supported platforms.
        // See https://learn.microsoft.com/en-us/dotnet/api/system.io.path.directoryseparatorchar#remarks

        if (Path.DirectorySeparatorChar != DirectorySeparatorChar &&
            Path.AltDirectorySeparatorChar != DirectorySeparatorChar)
        {
            // This is pretty impossible to reach, since Windows, Linux, macOS and
            // other Unix-based systems have the forward slash '/' as either the main
            // directory separator or, at the very least, as the alt directory separator.
            throw new PlatformNotSupportedException(
                "The current platform doesn't support the forward slash as a directory separator!" +
                $"Supported directory separators are: '{Path.DirectorySeparatorChar}' and {Path.AltDirectorySeparatorChar}");
        }
    }

    /// <summary>
    /// Character used to separate directory levels in a path that reflects a hierarchical file system organization.
    /// </summary>
    /// <seealso cref="DirectorySeparatorString"/>
    public const char DirectorySeparatorChar = '/';

    /// <summary>
    /// <see cref="DirectorySeparatorChar"/> as a string.
    /// </summary>
    /// <seealso cref="DirectorySeparatorChar"/>
    public const string DirectorySeparatorString = "/";

    /// <summary>
    /// Character used to separate extensions from the file name.
    /// </summary>
    public const char ExtensionSeparatorChar = '.';

    /// <summary>
    /// Debug assert sanitization.
    /// </summary>
    [Conditional("DEBUG")]
    [ExcludeFromCodeCoverage(Justification = $"{nameof(IsSanitized)} is tested separately.")]
    public static void DebugAssertIsSanitized(ReadOnlySpan<char> path)
    {
        Debug.Assert(IsSanitized(path), $"Path is not sanitized: '{path.ToString()}'");
    }

    /// <summary>
    /// Asserts whether the path is rooted depending on <paramref name="shouldBeRooted"/>.
    /// </summary>
    /// <exception cref="PathException">Thrown when relative paths are rooted or absolute paths aren't rooted.</exception>
    public static void AssertIsRooted(ReadOnlySpan<char> input, bool shouldBeRooted)
    {
        if (input.IsEmpty) return;

        var isRooted = IsRooted(input);
        if (isRooted && !shouldBeRooted) throw new PathException($"Relative paths can't be rooted: `{input.ToString()}`");
        if (!isRooted && shouldBeRooted) throw new PathException($"Absolute paths must be rooted: `{input.ToString()}`");
    }

    /// <summary>
    /// Determines whether the path is sanitized or not. Only sanitized paths should
    /// be used with <see cref="PathHelpers"/>.
    /// </summary>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static bool IsSanitized(ReadOnlySpan<char> path, IOSInformation os, bool isRelative) => IsSanitized(path);

    /// <summary>
    /// Removes trailing directory separator characters from the input.
    /// </summary>
    public static ReadOnlySpan<char> RemoveTrailingDirectorySeparator(ReadOnlySpan<char> path)
    {
        Debug.Assert(path.Length > 1);
        return path.DangerousGetReferenceAt(path.Length - 1) == DirectorySeparatorChar
            ? path.SliceFast(0, path.Length - 1)
            : path;
    }

    /// <summary>
    /// Sanitizes the given path. Only sanitized paths should be used with
    /// <see cref="PathHelpers"/>.
    /// </summary>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static string Sanitize(ReadOnlySpan<char> path, IOSInformation os, bool isRelative) => Sanitize(path);

    /// <summary>
    /// Gets the root type of a path.
    /// </summary>
    public static PathRootType GetRootType(ReadOnlySpan<char> path) => GetPathRoot(path).RootType;

    /// <summary>
    /// Returns whether the given path is rooted.
    /// </summary>
    public static bool IsRooted(ReadOnlySpan<char> path) => GetRootType(path) != PathRootType.None;

    /// <summary>
    /// Returns whether the given path is rooted.
    /// </summary>
    public static bool IsRooted(ReadOnlySpan<char> path, out PathRootType rootType)
    {
        rootType = GetRootType(path);
        return rootType != PathRootType.None;
    }

    /// <summary>
    /// Gets the length of the root part or <c>-1</c> if the path isn't rooted.
    /// </summary>
    public static int GetRootLength(ReadOnlySpan<char> path)
    {
        var pathRoot = GetPathRoot(path);
        return pathRoot.RootType == PathRootType.None ? -1 : pathRoot.Span.Length;
    }

    /// <summary>
    /// Returns whether the given path is a root.
    /// </summary>
    public static bool IsRootDirectory(ReadOnlySpan<char> path) => GetRootLength(path) == path.Length;

    /// <summary>
    /// Returns the root part of a path.
    /// </summary>
    /// <exception cref="PathException">Thrown for invalid paths</exception>
    public static PathRoot GetPathRoot(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return new PathRoot(ReadOnlySpan<char>.Empty, PathRootType.None);

        // DOS paths and relative paths don't start with a `/`
        if (path.DangerousGetReferenceAt(0) is not DirectorySeparatorChar)
        {
            // check for DOS path `C:/`
            if (path.Length < PathRoot.DOSRootLength) return new PathRoot(ReadOnlySpan<char>.Empty, PathRootType.None);

            var hasVolumeSeparator = path.DangerousGetReferenceAt(1) is PathRoot.WindowsVolumeSeparatorChar;
            var hasDirectorySeparator = path.DangerousGetReferenceAt(2) is DirectorySeparatorChar;
            if (!hasVolumeSeparator || !hasDirectorySeparator) return new PathRoot(ReadOnlySpan<char>.Empty, PathRootType.None);

            var windowsDriveChar = path.DangerousGetReferenceAt(0);
            if (!IsValidWindowsDriveChar(windowsDriveChar)) throw new PathException($"Path contains invalid windows drive character: `{path.ToString()}` (`{windowsDriveChar}`)");
            return new PathRoot(path.SliceFast(start: 0, length: PathRoot.DOSRootLength), PathRootType.DOS);
        }

        if (path.Length == 1) return new PathRoot(path.SliceFast(start: 0, length: 1), PathRootType.Unix);

        // UNC and DOS device paths start with `//`
        if (path.DangerousGetReferenceAt(1) is not DirectorySeparatorChar) return new PathRoot(path.SliceFast(start: 0, length: 1), PathRootType.Unix);

        // path starts with `//` and then has a random character, that's not valid
        if (path.Length < PathRoot.MinUNCRootLength) throw new PathException($"Path is too small to be a valid rooted path: `{path.ToString()}`");

        // DOS device paths start with either `//./` or `//?/`
        var dosDevicePathSeparatorChar = path.DangerousGetReferenceAt(2);
        var hasDOSDevicePathSeparatorChar = dosDevicePathSeparatorChar is '.' or '?';
        var isDOSDevicePath = path.Length >= PathRoot.DOSDeviceDriveRootLength && hasDOSDevicePathSeparatorChar && path.DangerousGetReferenceAt(3) is DirectorySeparatorChar;

        if (!isDOSDevicePath)
        {
            // check if UNC `//Server/foo`
            var slice = path.SliceFast(start: 2);
            var separatorIndex = slice.IndexOf(DirectorySeparatorChar);
            if (separatorIndex == -1) throw new PathException($"Invalid UNC path, missing directory separator: `{path.ToString()}`");

            Debug.Assert(path.Length >= 2 + separatorIndex + 1);
            var rootPart = path.SliceFast(start: 0, length: 2 + separatorIndex + 1);
            return new PathRoot(rootPart, PathRootType.UNC);
        }

        // check for DOS device drive paths `//./C:/`
        if (path.DangerousGetReferenceAt(5) is PathRoot.WindowsVolumeSeparatorChar && path.DangerousGetReferenceAt(6) is DirectorySeparatorChar)
        {
            var windowsDriveChar = path.DangerousGetReferenceAt(4);
            if (!IsValidWindowsDriveChar(windowsDriveChar)) throw new PathException($"Path contains invalid windows drive character: `{path.ToString()}` (`{windowsDriveChar}`)");
            return new PathRoot(path.SliceFast(start: 0, length: PathRoot.DOSDeviceDriveRootLength), PathRootType.DOSDeviceDrive);
        }

        if (path.Length < PathRoot.DOSDeviceVolumeRootLength) throw new PathException($"Path is not a valid DOS Device Volume path: `{path.ToString()}`");

        var hasVolumePrefix = path.SliceFast(start: PathRoot.DOSDevicePrefixLength, length: PathRoot.DOSDeviceVolumePrefix.Length).SequenceEqual(PathRoot.DOSDeviceVolumePrefix);
        if (!hasVolumePrefix) throw new PathException($"Path is missing DOS Device Volume prefix: `{path.ToString()}`");

        if (path.DangerousGetReferenceAt(PathRoot.DOSDeviceVolumeRootLength - 2) is not '}') throw new PathException($"Invalid DOS Device Volume path, missing directory separator: `{path.ToString()}`");
        return new PathRoot(path.SliceFast(start: 0, length: PathRoot.DOSDeviceVolumeRootLength), PathRootType.DOSDeviceVolume);
    }

    /// <summary>
    /// Returns a sanitized path that is valid to be used with other methods in <see cref="PathHelpers"/>.
    /// </summary>
    public static string Sanitize(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;
        if (IsSanitized(input)) return input.ToString();

        // NOTE(erri120): sanitization does the following:
        // 1) Turns `\\` into `/`
        // 2) Removes duplicate directory separators, eg `/foo//bar` turns into `/foo/bar`
        // 3) Removes trailing directory separators from any path that isn't a root directory, eg `/foo/bar/` turns into `/foo/bar`

        var buffer = input.Length > 512
            ? GC.AllocateUninitializedArray<char>(input.Length)
            : stackalloc char[input.Length];

        var bufferIndex = 0;
        var previousWasDirectorySeparator = false;

        for (var inputIndex = 0; inputIndex < input.Length; inputIndex++)
        {
            var current = input.DangerousGetReferenceAt(inputIndex);
            current = current is '\\' ? DirectorySeparatorChar : current;

            var isDirectorySeparator = current is DirectorySeparatorChar;
            if (isDirectorySeparator && previousWasDirectorySeparator)
            {
                // two consecutive directory separators are only valid in these situations:
                // 1) UNC paths start with `\\`
                // 2) DOS device paths start with either `\\.\` or `\\?\`
                // as such, they are only allowed in the beginning, otherwise
                // we'll skip them
                if (inputIndex != 1) continue;

                // NOTE(erri120): dumb case where you can provide `//`
                if (input.Length == 2) return DirectorySeparatorString;
            }

            buffer[bufferIndex++] = current;
            previousWasDirectorySeparator = isDirectorySeparator;
        }

        var slice = buffer.SliceFast(start: 0, length: bufferIndex).TrimEnd();

        // Don't remove the trailing directory separator for root directories
        var result = IsRootDirectory(slice) ? slice : RemoveTrailingDirectorySeparator(slice);
        return result.ToString();
    }

    private static SearchValues<char> _invalidUnicodeCharacters = SearchValues.Create(new []{
        '\uFFFD', // � REPLACEMENT CHARACTER used to replace an unknown, unrecognised, or unrepresentable character
        '\uFFFE', // <noncharacter-FFFE> not a character
        '\uFFFF', // <noncharacter-FFFF> not a character
    });

    /// <summary>
    /// Returns whether the input is sanitized.
    /// </summary>
    public static bool IsSanitized(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return true;
        if (input.ContainsAny(_invalidUnicodeCharacters)) throw new PathException($"Input contains invalid characters: `{input}` (length={input.Length})");
        if (SpanExtensions.Count(input, '\\') != 0) return false;

        var doubleDirectorySeparatorIndex = input.LastIndexOf("//", StringComparison.Ordinal);
        if (doubleDirectorySeparatorIndex > 0 || (doubleDirectorySeparatorIndex == 0 && input.Length == 2)) return false;

        var last = input.DangerousGetReferenceAt(input.Length - 1);
        if (last == ' ') return false;
        if (IsRootDirectory(input)) return true;
        return last is not DirectorySeparatorChar;
    }

    /// <summary>
    /// Replaces all directory separator characters with the
    /// native directory separator character of the passed OS.
    /// <remarks>
    /// Assumes sanitized path, changes to `/` on Unix-based systems and `\` on Windows.
    /// </remarks>
    /// </summary>
    [SkipLocalsInit]
    public static string ToNativeSeparators(ReadOnlySpan<char> path, IOSInformation os)
    {
        DebugAssertIsSanitized(path);
        if (path.IsEmpty) return string.Empty;

        if (os.IsUnix()) return path.ToString();

        var buffer = path.Length > 512
            ? GC.AllocateUninitializedArray<char>(path.Length)
            : stackalloc char[path.Length];
        path.CopyTo(buffer);
        return buffer.Replace('/', '\\', buffer).ToString();
    }

    /// <summary>
    /// Checks for equality between two paths.
    /// </summary>
    /// <remarks>
    /// Equality of paths is handled case-insensitive, meaning "/foo" is equal to "/FOO".
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PathEquals(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        DebugAssertIsSanitized(left);
        DebugAssertIsSanitized(right);

        if (left.IsEmpty && right.IsEmpty) return true;
        if (left.IsEmpty && !right.IsEmpty || right.IsEmpty && !left.IsEmpty) return false;
        return left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compares two paths.
    /// </summary>
    /// <remarks>
    /// Path comparisons are handled case-insensitive, meaning "/foo" is equal to "/FOO".
    /// </remarks>
    /// <returns>
    /// A signed integer that indicates the relative order of <paramref name="left" /> and <paramref name="right" />:
    /// <br />   - If less than 0, <paramref name="left" /> precedes <paramref name="right" />.
    /// <br />   - If 0, <paramref name="left" /> equals <paramref name="right" />.
    /// <br />   - If greater than 0, <paramref name="left" /> follows <paramref name="right" />.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        DebugAssertIsSanitized(left);
        DebugAssertIsSanitized(right);
        return left.CompareTo(right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the hash code of the input path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PathHashCode(string input)
    {
        DebugAssertIsSanitized(input);
        return StringComparer.OrdinalIgnoreCase.GetHashCode(input);
    }

    /// <summary>
    /// Returns true if the given character is a valid Windows drive letter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidWindowsDriveChar(char value)
    {
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
        // source: https://github.com/dotnet/runtime/blob/d9f453924f7c3cca9f02d920a57e1477293f216e/src/libraries/Common/src/System/IO/PathInternal.Windows.cs#L69-L75
        return (uint)((value | 0x20) - 'a') <= 'z' - 'a';
    }

    /// <inheritdoc cref="GetRootLength(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static int GetRootLength(ReadOnlySpan<char> path, IOSInformation os) => GetRootLength(path);

    /// <inheritdoc cref="IsRooted(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static bool IsRooted(ReadOnlySpan<char> path, IOSInformation os) => IsRooted(path);

    /// <inheritdoc cref="GetPathRoot"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static ReadOnlySpan<char> GetRootPart(ReadOnlySpan<char> path, IOSInformation os) => GetPathRoot(path).Span;

    /// <inheritdoc cref="IsRootDirectory(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static bool IsRootDirectory(ReadOnlySpan<char> path, IOSInformation os) => IsRootDirectory(path);

    /// <summary>
    /// Calculates the exact length required for a buffer to contain the result of
    /// joining two path parts.
    /// </summary>
    /// <seealso cref="GetMaxJoinedPartLength"/>
    public static int GetExactJoinedPartLength(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.IsEmpty) return right.Length;
        if (right.IsEmpty) return left.Length;
        if (left.DangerousGetReferenceAt(left.Length - 1) == DirectorySeparatorChar)
            return left.Length + right.Length;
        return left.Length + DirectorySeparatorString.Length + right.Length;
    }

    /// <summary>
    /// Gets the maximum length required for a buffer to contain the result of
    /// joining two path parts using.
    /// </summary>
    /// <remarks>
    /// This method differs from <see cref="GetExactJoinedPartLength"/> in that it's the
    /// maximum amount, rather than the exact amount required. Using the maximum amount
    /// means potentially allocating more memory than required.
    /// </remarks>
    /// <seealso cref="GetExactJoinedPartLength"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxJoinedPartLength(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        return left.Length + DirectorySeparatorString.Length + right.Length;
    }

    /// <summary>
    /// Joins two path parts together and writes the result to a buffer.
    /// </summary>
    /// <remarks>
    /// This method returns the amount of written characters to the buffer.
    /// It's the responsibility of the caller to allocate enough memory for the buffer.
    /// Use <see cref="GetExactJoinedPartLength"/> to get an accurate length or
    /// <see cref="GetMaxJoinedPartLength"/> to get the maximum length.
    /// </remarks>
    /// <returns>The amount of written characters.</returns>
    public static int JoinParts(Span<char> buffer, ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        DebugAssertIsSanitized(left);
        DebugAssertIsSanitized(right);
        Debug.Assert(buffer.Length >= GetExactJoinedPartLength(left, right), $"Buffer has a size of '{buffer.Length}' but requires at least '{GetExactJoinedPartLength(left, right)}'");

        if (left.IsEmpty)
        {
            if (right.IsEmpty) return 0;

            right.CopyTo(buffer);
            return right.Length;
        }

        if (right.IsEmpty)
        {
            left.CopyTo(buffer);
            return left.Length;
        }

        if (left.DangerousGetReferenceAt(left.Length - 1) == DirectorySeparatorChar)
        {
            left.CopyTo(buffer);
            right.CopyTo(buffer.SliceFast(left.Length));
            return left.Length + right.Length;
        }

        left.CopyTo(buffer);

        ref var c = ref buffer.DangerousGetReferenceAt(left.Length);
        c = DirectorySeparatorChar;

        right.CopyTo(buffer.SliceFast(left.Length + DirectorySeparatorString.Length));

        return left.Length + DirectorySeparatorString.Length + right.Length;
    }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    /// <summary>
    /// Joins two path parts together and returns the joined path as a string.
    /// </summary>
    /// <param name="left">The left part of the path as a <see cref="ReadOnlySpan{T}"/></param>
    /// <param name="right">The right part of the path as a <see cref="ReadOnlySpan{T}"/></param>
    /// <returns>The joined path.</returns>
    public static string JoinParts(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        DebugAssertIsSanitized(left);
        DebugAssertIsSanitized(right);

        var spanLength = GetExactJoinedPartLength(left, right);
        unsafe
        {
            // Note: The two Span objects are on the Stack. We access them inside
            // string.Create, by dereferencing these items from the stack.

            // If a GC happens, the pointers inside these referenced items will be
            // moved, but our stack objects won't. Therefore, access like this without
            // an explicit pin is safe.
            // A similar trick also exists out there known as 'ref pinning'.

            // Don't believe me? Go crazy with `DOTNET_GCStress` 😉 - Sewer
            var @params = new JoinPartsParams
            {
                Left = &left,
                Right = &right,
            };

            return string.Create(spanLength, @params, (span, tuple) =>
            {
                var count = JoinParts(span, *tuple.Left, *tuple.Right);
                Debug.Assert(count == spanLength, $"Calculated span length '{spanLength}' doesn't match actual span length '{count}'");
            });
        }
    }

    unsafe struct JoinPartsParams
    {
        internal ReadOnlySpan<char>* Left;
        internal ReadOnlySpan<char>* Right;
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

    /// <summary>
    /// Joins two path parts together and returns the joined path as a string.
    /// </summary>
    /// <remarks>
    /// This method uses strings as inputs.
    /// instead.
    /// </remarks>
    /// <param name="left">The left part of the path as a <see cref="string"/></param>
    /// <param name="right">The right part of the path as a <see cref="string"/></param>
    /// <returns>The joined path.</returns>
    public static string JoinParts(string left, string right)
    {
        DebugAssertIsSanitized(left);
        DebugAssertIsSanitized(right);

        var spanLength = GetExactJoinedPartLength(left, right);
        return string.Create(spanLength, (left, right), static (span, tuple) =>
        {
            // ReSharper disable InconsistentNaming
            var (left_, right_) = tuple;
            // ReSharper restore InconsistentNaming

            var count = JoinParts(span, left_, right_);
            Debug.Assert(count == span.Length, $"Calculated span length '{span.Length}' doesn't match actual span length '{count}'");
        });
    }

    /// <summary>
    /// Returns the file name of the given path or <see cref="ReadOnlySpan{T}.Empty"/>
    /// if there is no file name.
    /// </summary>
    /// <remarks>
    /// The file name is the last part of the path, after the last
    /// directory separator character. As such, if the path ends
    /// with a directory separator, the result will be <see cref="ReadOnlySpan{T}.Empty"/>.
    /// </remarks>
    /// <returns></returns>
    public static ReadOnlySpan<char> GetFileName(ReadOnlySpan<char> path)
    {
        DebugAssertIsSanitized(path);

        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;
        var pathRoot = GetPathRoot(path);
        if (pathRoot.Length == path.Length) return ReadOnlySpan<char>.Empty;

        for (var i = path.Length; --i >= 0;)
        {
            if (path.DangerousGetReferenceAt(i) != DirectorySeparatorChar) continue;
            return path.SliceFast(i + 1);
        }

        return path;
    }

    /// <summary>
    /// Returns the extension of the given path, or <see cref="ReadOnlySpan{T}.Empty"/>
    /// if there is no extension. The returned extension will always start with <see cref="ExtensionSeparatorChar"/>.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;
        if (path.DangerousGetReferenceAt(path.Length - 1) == ExtensionSeparatorChar) return ReadOnlySpan<char>.Empty;

        for (var i = path.Length; --i >= 0;)
        {
            if (path.DangerousGetReferenceAt(i) != ExtensionSeparatorChar) continue;
            return path.SliceFast(i);
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Replaces the extension of the old path with the new extension.
    /// </summary>
    /// <param name="oldPath"></param>
    /// <param name="newExtension"></param>
    /// <returns></returns>
    public static string ReplaceExtension(string oldPath, string newExtension)
    {
        var oldPathSpan = oldPath.AsSpan();
        if (oldPathSpan.IsEmpty) return string.Empty;

        int i;
        for (i = oldPathSpan.Length; --i >= 0;)
        {
            if (oldPathSpan.DangerousGetReferenceAt(i) == ExtensionSeparatorChar) break;
        }

        var oldPathWithoutExtensionLength = i > 0 ? i : oldPathSpan.Length;
        var newPathLength = oldPathWithoutExtensionLength + newExtension.Length;

        return string.Create(newPathLength, (oldPathWithoutExtensionLength, oldPath, newExtension), static (span, tuple) =>
        {
            // ReSharper disable InconsistentNaming
            var (length, oldPath_, newExtension_) = tuple;
            // ReSharper restore InconsistentNaming

            var slice = oldPath_.AsSpan().SliceFast(0, length);
            slice.CopyTo(span);
            newExtension_.CopyTo(span.SliceFast(length));
        });
    }

    /// <inheritdoc cref="GetDirectoryDepth(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static int GetDirectoryDepth(ReadOnlySpan<char> path, IOSInformation os) => GetDirectoryDepth(path);

    /// <summary>
    /// Calculates the depth of a path.
    /// </summary>
    /// <remarks>
    /// The depth of a path is defined by the numbers of directories it has.
    /// </remarks>
    public static int GetDirectoryDepth(ReadOnlySpan<char> input)
    {
        DebugAssertIsSanitized(input);
        if (input.IsEmpty) return 0;

        var pathRoot = GetPathRoot(input);
        if (pathRoot.Length == input.Length) return 1;

        var slice = pathRoot.RootType == PathRootType.None ? input : input.SliceFast(start: pathRoot.Length);
        var count = SpanExtensions.Count(slice, DirectorySeparatorChar);
        var root = pathRoot.RootType == PathRootType.None ? 0 : 1;
        return count + root;
    }

    /// <inheritdoc cref="GetDirectoryName(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path, IOSInformation os) => GetDirectoryName(path);

    /// <summary>
    /// Returns the directory name of the given path, also known as the parent.
    /// </summary>
    public static ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> input)
    {
        DebugAssertIsSanitized(input);
        if (input.IsEmpty) return ReadOnlySpan<char>.Empty;

        var pathRoot = GetPathRoot(input);

        // NOTE(erri120): parent of the root is the root, this is to prevent infinite while loops when traversing
        if (pathRoot.Length == input.Length) return input;

        for (var i = input.Length; --i >= 0;)
        {
            if (input.DangerousGetReferenceAt(i) != DirectorySeparatorChar) continue;
            return input.SliceFast(start: 0, i == pathRoot.Length - 1 ? pathRoot.Length : i);
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Determines whether <paramref name="child"/> is in folder <paramref name="parent"/>.
    /// </summary>
    /// <remarks>
    /// This method will return <c>false</c>, if either <paramref name="child"/> or <paramref name="parent"/> are empty.
    /// </remarks>
    public static bool InFolder(ReadOnlySpan<char> child, ReadOnlySpan<char> parent)
    {
        DebugAssertIsSanitized(child);
        DebugAssertIsSanitized(parent);

        if (parent.IsEmpty || child.IsEmpty && parent.IsEmpty) return true;
        if (child.IsEmpty) return false;
        if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return false;

        if (child.Length == parent.Length) return true;
        if (IsRootDirectory(parent)) return true;
        return child.DangerousGetReferenceAt(parent.Length) == DirectorySeparatorChar;
    }

    /// <inheritdoc cref="InFolder(System.ReadOnlySpan{char}, System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static bool InFolder(ReadOnlySpan<char> child, ReadOnlySpan<char> parent, IOSInformation os) => InFolder(child, parent);

    /// <summary>
    /// Returns the part from <paramref name="child"/> that is relative to <paramref name="parent"/>.
    /// </summary>
    /// <remarks>
    /// This method will return <see cref="ReadOnlySpan{T}.Empty"/> if <paramref name="child"/> is
    /// not relative to <paramref name="parent"/>.
    /// </remarks>
    public static ReadOnlySpan<char> RelativeTo(ReadOnlySpan<char> child, ReadOnlySpan<char> parent)
    {
        DebugAssertIsSanitized(child);
        DebugAssertIsSanitized(parent);

        if (child.IsEmpty && parent.IsEmpty) return ReadOnlySpan<char>.Empty;
        if (!InFolder(child, parent)) return ReadOnlySpan<char>.Empty;

        return IsRootDirectory(parent)
            ? child.SliceFast(parent.Length)
            : child.SliceFast(parent.Length + DirectorySeparatorString.Length);
    }

    /// <inheritdoc cref="RelativeTo(System.ReadOnlySpan{char}, System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static ReadOnlySpan<char> RelativeTo(ReadOnlySpan<char> child, ReadOnlySpan<char> parent, IOSInformation os) => RelativeTo(child, parent);

    /// <summary>
    /// Returns the first directory in the path.
    /// </summary>
    public static ReadOnlySpan<char> GetTopParent(ReadOnlySpan<char> path)
    {
        DebugAssertIsSanitized(path);

        var pathRoot = GetPathRoot(path);
        if (pathRoot.RootType != PathRootType.None) return pathRoot.Span;

        var index = path.IndexOf(DirectorySeparatorChar);
        return index == -1 ? path : path.SliceFast(start: 0, length: index);
    }

    /// <inheritdoc cref="GetTopParent(System.ReadOnlySpan{char})"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static ReadOnlySpan<char> GetTopParent(ReadOnlySpan<char> path, IOSInformation os) => GetTopParent(path);

    /// <summary>
    /// Drops the first <paramref name="count"/> parents of the given path.
    /// </summary>
    public static ReadOnlySpan<char> DropParents(ReadOnlySpan<char> path, int count)
    {
        DebugAssertIsSanitized(path);
        ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));

        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;
        if (count == 0) return path;

        var pathRoot = GetPathRoot(path);
        // can't drop parents of root directories
        if (pathRoot.Length == path.Length) return ReadOnlySpan<char>.Empty;

        // start after the root
        var res = pathRoot.RootType == PathRootType.None ? path : path.SliceFast(start: pathRoot.Length);
        count = pathRoot.RootType == PathRootType.None ? count : count - 1;

        for (var x = 0; x < count; x++)
        {
            var index = res.IndexOf(DirectorySeparatorChar);
            if (index == -1) return ReadOnlySpan<char>.Empty;

            res = res.SliceFast(index + 1);
        }

        return res;
    }

    /// <inheritdoc cref="DropParents(System.ReadOnlySpan{char},int)"/>
    [Obsolete($"Path methods providing {nameof(IOSInformation)} are deprecated")]
    public static ReadOnlySpan<char> DropParents(ReadOnlySpan<char> path, int count, IOSInformation os) => DropParents(path, count);

    /// <summary>
    /// Delegate used with <see cref="PathHelpers.WalkParts"/>.
    /// </summary>
    /// <seealso cref="WalkPartDelegate{TState}"/>
    public delegate bool WalkPartDelegate(ReadOnlySpan<char> part);

    /// <summary>
    /// Delegate used with <seealso cref="PathHelpers.WalkParts{TState}"/>
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <seealso cref="WalkPartDelegate"/>
    public delegate bool WalkPartDelegate<TState>(ReadOnlySpan<char> part, ref TState state);

    /// <summary>
    /// Walks the parts of a path, invoking <paramref name="partDelegate"/> with each part of the path.
    /// </summary>
    /// <seealso cref="WalkParts{TState}"/>
    public static void WalkParts(ReadOnlySpan<char> path, WalkPartDelegate partDelegate, bool reverse = false)
    {
        WalkParts(path, ref reverse, (ReadOnlySpan<char> part, ref bool _) => partDelegate(part), reverse);
    }

    /// <summary>
    /// Walks the parts of a path, invoking <paramref name="partDelegate"/> with each part of the path.
    /// </summary>
    /// <remarks>
    /// The path <c>/foo</c> has the parts <c>/</c> and <c>foo</c>.
    /// </remarks>
    /// <param name="path">The path to walk.</param>
    /// <param name="state">The state to pass to the <paramref name="partDelegate"/></param>
    /// <param name="partDelegate">The delegate to invoke with each part and state.</param>
    /// <param name="reverse">Whether to walk the path forward or backwards.</param>
    /// <typeparam name="TState"></typeparam>
    /// <seealso cref="WalkParts"/>
    public static void WalkParts<TState>(
        ReadOnlySpan<char> path,
        ref TState state,
        WalkPartDelegate<TState> partDelegate,
        bool reverse = false)
    {
        DebugAssertIsSanitized(path);

        if (path.IsEmpty)
        {
            partDelegate(ReadOnlySpan<char>.Empty, ref state);
            return;
        }

        var rootLength = GetRootLength(path);
        if (path.Length == rootLength)
        {
            partDelegate(path, ref state);
            return;
        }

        if (reverse) WalkPartsBackwards(path, rootLength, partDelegate, ref state);
        else WalkPartsForward(path, rootLength, partDelegate, ref state);
    }

    private static void WalkPartsForward<TState>(ReadOnlySpan<char> path, int rootLength, WalkPartDelegate<TState> @delegate, ref TState state)
    {
        var previous = 0;
        for (var i = 0; i < path.Length; i++)
        {
            if (path.DangerousGetReferenceAt(i) != DirectorySeparatorChar) continue;
            if (i + 1 == rootLength)
            {
                var slice = path.SliceFast(0, rootLength);
                if (!@delegate(slice, ref state)) return;
                previous = i + 1;
            }
            else
            {
                var slice = path.SliceFast(previous, i - previous);
                if (!@delegate(slice, ref state)) return;
                previous = i + 1;
            }
        }

        var rest = path.SliceFast(previous);
        @delegate(rest, ref state);
    }

    private static void WalkPartsBackwards<TState>(ReadOnlySpan<char> path, int rootLength, WalkPartDelegate<TState> @delegate, ref TState state)
    {
        var previous = path.Length;
        for (var i = path.Length; --i >= 0;)
        {
            if (path.DangerousGetReferenceAt(i) != DirectorySeparatorChar) continue;

            if (i + 1 == rootLength)
            {
                var slice = path.SliceFast(rootLength, previous - rootLength);
                if (!@delegate(slice, ref state)) return;
                previous = i + 1;
            }
            else
            {
                var slice = path.SliceFast(i + 1, previous - i - 1);
                if (!@delegate(slice, ref state)) return;
                previous = i;
            }
        }

        var rest = path.SliceFast(0, previous);
        @delegate(rest, ref state);
    }

    /// <summary>
    /// Returns a read-only list containing all parts of the given path.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="reverse"></param>
    /// <returns></returns>
    /// <seealso cref="WalkParts"/>
    /// <seealso cref="WalkParts{TState}"/>
    public static IReadOnlyList<string> GetParts(ReadOnlySpan<char> path, bool reverse = false)
    {
        DebugAssertIsSanitized(path);

        var list = new List<string>();

        WalkParts(path, ref list, (ReadOnlySpan<char> part, ref List<string> output) =>
        {
            output.Add(part.ToString());
            return true;
        }, reverse);

        return list;
    }
}
