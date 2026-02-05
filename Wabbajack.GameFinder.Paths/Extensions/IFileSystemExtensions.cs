using System;
using System.Text;

namespace Wabbajack.GameFinder.Paths.Extensions;

/// <summary>
/// Extensions for <see cref="IFileSystem"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public static class IFileSystemExtensions
{
    /// <summary>
    /// Expands the known folders in the input path, possible known folders are:
    /// {EntryFolder}, {CurrentDirectory}, {HomeFolder}, {MyGames}
    /// </summary>
    public static AbsolutePath ExpandKnownFoldersPath(this IFileSystem fileSystem, string inputPath)
    {
        var sb = new StringBuilder(inputPath);
        sb.Replace("{EntryFolder}", fileSystem.GetKnownPath(KnownPath.EntryDirectory).GetFullPath());
        sb.Replace("{CurrentDirectory}", fileSystem.GetKnownPath(KnownPath.CurrentDirectory).GetFullPath());
        sb.Replace("{HomeFolder}", fileSystem.GetKnownPath(KnownPath.HomeDirectory).GetFullPath());
        sb.Replace("{MyGames}", fileSystem.GetKnownPath(KnownPath.MyGamesDirectory).GetFullPath());

        var result = sb.ToString();
        return fileSystem.FromUnsanitizedFullPath(result);
    }
}
