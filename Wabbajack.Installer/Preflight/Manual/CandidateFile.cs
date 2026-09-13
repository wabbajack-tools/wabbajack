using System;
using System.Collections.Generic;
using System.IO;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     The rules that decide whether a file in the watch folder is worth looking at. Pure, so that each rule
///     can be tabled in a test; the acquirer supplies the file system facts.
/// </summary>
public static class CandidateFile
{
    /// <summary>
    ///     Files a browser or download manager is still writing, plus this application's own transient files.
    /// </summary>
    public static readonly IReadOnlySet<Extension> DefaultPartialExtensions = new HashSet<Extension>
    {
        new(".crdownload"),
        new(".part"),
        new(".partial"),
        new(".tmp"),
        new(".temp"),
        new(".download"),
        new(".opdownload"),
        new(".aria2"),
        new(".!ut"),
        new(".!qb"),
        new(".wj_incoming"),
        new(".meta")
    };

    /// <summary>
    ///     <c>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS</c>: the file's bytes live in the cloud and reading them
    ///     starts a download. Not in <see cref="FileAttributes" />.
    /// </summary>
    public const int RecallOnDataAccess = 0x00400000;

    public static bool IsPartialDownload(AbsolutePath path, IReadOnlySet<Extension>? partialExtensions = null)
    {
        return (partialExtensions ?? DefaultPartialExtensions).Contains(path.Extension);
    }

    /// <summary>
    ///     Firefox creates the final file name as an empty placeholder and writes to <c>name.ext.part</c>
    ///     beside it; the placeholder is not a candidate while the partial exists.
    /// </summary>
    public static bool HasPartialSibling(AbsolutePath path, Func<AbsolutePath, bool> fileExists,
        IReadOnlySet<Extension>? partialExtensions = null)
    {
        foreach (var ext in partialExtensions ?? DefaultPartialExtensions)
        {
            if (fileExists(path.WithExtension(ext)))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     A file whose contents are not on the local disk. Hashing one would pull it down in full, which for a
    ///     multi-gigabyte archive is the wrong side effect to trigger silently, so these are reported instead.
    /// </summary>
    public static bool IsCloudPlaceholder(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.Offline) != 0) return true;
        if (((int) attributes & RecallOnDataAccess) != 0) return true;
        const FileAttributes reparseSparse = FileAttributes.ReparsePoint | FileAttributes.SparseFile;
        return (attributes & reparseSparse) == reparseSparse;
    }

    public static bool IsIgnoredAttributes(FileAttributes attributes)
    {
        const FileAttributes ignored = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory;
        return (attributes & ignored) != 0;
    }
}
