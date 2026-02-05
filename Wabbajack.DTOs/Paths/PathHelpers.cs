using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Wabbajack.Paths;

/// <summary>
/// Helper methods for path manipulation.
/// AbsolutePath uses forward slash internally, RelativePath uses backslash for Windows modlist compatibility.
/// </summary>
public static class PathHelpers
{
    /// <summary>
    /// Directory separator used for AbsolutePath (forward slash, works on all platforms).
    /// </summary>
    public const char ForwardSlash = '/';

    /// <summary>
    /// Directory separator used for RelativePath (backslash, for Windows modlist compatibility).
    /// </summary>
    public const char BackSlash = '\\';

    /// <summary>
    /// Extension separator character.
    /// </summary>
    public const char ExtensionSeparatorChar = '.';

    private static readonly HashSet<char> DriveLetters = new("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    private static readonly SearchValues<char> InvalidUnicodeCharacters = SearchValues.Create(new[]
    {
        '\uFFFD', // REPLACEMENT CHARACTER
        '\uFFFE', // not a character
        '\uFFFF', // not a character
    });

    /// <summary>
    /// Sanitizes an absolute path: converts backslashes to forward slashes, removes duplicate separators,
    /// removes trailing separators (except for root directories).
    /// </summary>
    public static string SanitizeAbsolute(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;
        if (IsSanitized(input, ForwardSlash)) return input.ToString();

        return SanitizeCore(input, ForwardSlash, BackSlash);
    }

    /// <summary>
    /// Sanitizes a relative path: converts forward slashes to backslashes, removes duplicate separators,
    /// removes trailing separators.
    /// </summary>
    public static string SanitizeRelative(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;
        if (IsSanitized(input, BackSlash)) return input.ToString();

        return SanitizeCore(input, BackSlash, ForwardSlash);
    }

    private static string SanitizeCore(ReadOnlySpan<char> input, char targetSep, char sourceSep)
    {
        Span<char> buffer = input.Length > 512
            ? new char[input.Length]
            : stackalloc char[input.Length];

        var bufferIndex = 0;
        var previousWasSeparator = false;

        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            current = current == sourceSep ? targetSep : current;

            var isSeparator = current == targetSep;
            if (isSeparator && previousWasSeparator)
            {
                // Allow double separator at start for UNC paths (Windows)
                if (i != 1) continue;
                if (input.Length == 2) return targetSep.ToString();
            }

            buffer[bufferIndex++] = current;
            previousWasSeparator = isSeparator;
        }

        var slice = buffer.Slice(0, bufferIndex).TrimEnd();

        // Remove trailing separator unless it's a root directory
        if (slice.Length > 1 && slice[^1] == targetSep)
        {
            // Check if it's a root directory (e.g., "C:/" or "/")
            if (!IsRootDirectorySpan(slice, targetSep))
            {
                slice = slice.Slice(0, slice.Length - 1);
            }
        }

        return slice.ToString();
    }

    private static bool IsSanitized(ReadOnlySpan<char> input, char expectedSep)
    {
        if (input.IsEmpty) return true;
        if (input.ContainsAny(InvalidUnicodeCharacters))
            throw new PathException($"Path contains invalid characters: `{input}`");

        var otherSep = expectedSep == ForwardSlash ? BackSlash : ForwardSlash;
        if (input.Contains(otherSep)) return false;

        // Check for duplicate separators
        var sepStr = new string(expectedSep, 2);
        var doubleSepIndex = input.LastIndexOf(sepStr, StringComparison.Ordinal);
        if (doubleSepIndex > 0 || (doubleSepIndex == 0 && input.Length == 2)) return false;

        var last = input[^1];
        if (last == ' ') return false;
        if (IsRootDirectorySpan(input, expectedSep)) return true;
        return last != expectedSep;
    }

    private static bool IsRootDirectorySpan(ReadOnlySpan<char> path, char sep)
    {
        if (path.IsEmpty) return false;
        if (sep == ForwardSlash)
        {
            // Unix root: "/"
            if (path.Length == 1 && path[0] == ForwardSlash) return true;
            // DOS root: "C:/"
            if (path.Length == 3 && DriveLetters.Contains(path[0]) && path[1] == ':' && path[2] == ForwardSlash)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Converts a relative path from backslashes to forward slashes for combining with AbsolutePath.
    /// </summary>
    public static string RelativeToForwardSlash(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        return relativePath.Replace(BackSlash, ForwardSlash);
    }

    /// <summary>
    /// Converts a path to native platform separators.
    /// </summary>
    public static string ToNativeSeparators(string path, bool isWindows)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return isWindows ? path.Replace(ForwardSlash, BackSlash) : path;
    }

    /// <summary>
    /// Gets the file name portion of a path (after the last separator).
    /// </summary>
    public static ReadOnlySpan<char> GetFileName(ReadOnlySpan<char> path, char separator)
    {
        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;

        for (var i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] == separator)
                return path.Slice(i + 1);
        }

        return path;
    }

    /// <summary>
    /// Gets the directory name portion of a path (before the last separator).
    /// </summary>
    public static ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path, char separator)
    {
        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;

        for (var i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] == separator)
            {
                // For root directories like "/" or "C:/", return the full path including separator
                if (separator == ForwardSlash)
                {
                    if (i == 0) return path.Slice(0, 1); // Unix root "/"
                    if (i == 2 && path.Length >= 3 && path[1] == ':') return path.Slice(0, 3); // DOS root "C:/"
                }
                return path.Slice(0, i);
            }
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Gets the extension from a path (including the dot).
    /// </summary>
    public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;
        if (path[^1] == ExtensionSeparatorChar) return ReadOnlySpan<char>.Empty;

        for (var i = path.Length - 1; i >= 0; i--)
        {
            if (path[i] == ExtensionSeparatorChar)
                return path.Slice(i);
            // Stop at directory separators
            if (path[i] == ForwardSlash || path[i] == BackSlash)
                return ReadOnlySpan<char>.Empty;
        }

        return ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Replaces the extension of a path with a new extension.
    /// </summary>
    public static string ReplaceExtension(string path, string newExtension)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var dotIndex = path.LastIndexOf(ExtensionSeparatorChar);
        if (dotIndex <= 0)
            return path + newExtension;

        // Make sure the dot is in the file name, not a directory
        var lastSepIndex = Math.Max(path.LastIndexOf(ForwardSlash), path.LastIndexOf(BackSlash));
        if (dotIndex < lastSepIndex)
            return path + newExtension;

        return path.Substring(0, dotIndex) + newExtension;
    }

    /// <summary>
    /// Joins two path parts with a separator.
    /// </summary>
    public static string JoinParts(string left, string right, char separator)
    {
        if (string.IsNullOrEmpty(left)) return right ?? string.Empty;
        if (string.IsNullOrEmpty(right)) return left;

        if (left[^1] == separator)
            return left + right;

        return left + separator + right;
    }

    /// <summary>
    /// Returns true if the character is a valid Windows drive letter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidWindowsDriveChar(char value)
    {
        return (uint)((value | 0x20) - 'a') <= 'z' - 'a';
    }

    /// <summary>
    /// Detects whether a path is Windows-style or Unix-style.
    /// </summary>
    public static bool IsWindowsPath(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return false;
        if (path[0] == ForwardSlash) return false;
        if (path.Length >= 2 && path[0] == BackSlash && path[1] == BackSlash) return true;
        if (path.Length >= 2 && DriveLetters.Contains(path[0]) && path[1] == ':') return true;
        return false;
    }

    /// <summary>
    /// Gets the depth (number of path components) of a path.
    /// </summary>
    public static int GetDepth(ReadOnlySpan<char> path, char separator)
    {
        if (path.IsEmpty) return 0;

        var count = 1;
        foreach (var c in path)
        {
            if (c == separator) count++;
        }

        // Don't count trailing separator
        if (path[^1] == separator) count--;

        return count;
    }

    /// <summary>
    /// Checks if a child path is within a parent folder.
    /// </summary>
    public static bool InFolder(ReadOnlySpan<char> child, ReadOnlySpan<char> parent, char separator)
    {
        if (parent.IsEmpty) return true;
        if (child.IsEmpty) return false;

        if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) return false;
        if (child.Length == parent.Length) return true;

        return child[parent.Length] == separator;
    }

    /// <summary>
    /// Gets the part of child that is relative to parent.
    /// </summary>
    public static ReadOnlySpan<char> RelativeTo(ReadOnlySpan<char> child, ReadOnlySpan<char> parent, char separator)
    {
        if (parent.IsEmpty) return child;
        if (child.IsEmpty) return ReadOnlySpan<char>.Empty;

        if (!InFolder(child, parent, separator)) return ReadOnlySpan<char>.Empty;

        if (child.Length == parent.Length) return ReadOnlySpan<char>.Empty;

        var startIndex = parent.Length;
        if (child[startIndex] == separator) startIndex++;

        return child.Slice(startIndex);
    }

    /// <summary>
    /// Gets the first part of a path (top parent).
    /// </summary>
    public static ReadOnlySpan<char> GetTopParent(ReadOnlySpan<char> path, char separator)
    {
        if (path.IsEmpty) return ReadOnlySpan<char>.Empty;

        var index = path.IndexOf(separator);
        if (index == -1) return path;
        if (index == 0) return path.Slice(0, 1); // Unix root

        return path.Slice(0, index);
    }

    /// <summary>
    /// Compares two paths case-insensitively.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        return left.CompareTo(right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if two paths are equal (case-insensitive).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PathEquals(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        return left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the hash code of a path (case-insensitive).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PathHashCode(string path)
    {
        return string.IsNullOrEmpty(path) ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(path);
    }

    /// <summary>
    /// Drops the first N parents from a path.
    /// </summary>
    public static ReadOnlySpan<char> DropParents(ReadOnlySpan<char> path, int count, char separator)
    {
        if (path.IsEmpty || count <= 0) return path;

        var result = path;
        for (var i = 0; i < count; i++)
        {
            var index = result.IndexOf(separator);
            if (index == -1) return ReadOnlySpan<char>.Empty;
            result = result.Slice(index + 1);
        }

        return result;
    }
}
