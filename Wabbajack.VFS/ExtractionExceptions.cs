using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wabbajack.Paths;

namespace Wabbajack.VFS;

/// <summary>
///     An archive whose contents no longer match the hash recorded for it. Raised once the file has been
///     re-read and the mismatch confirmed, so it states a fact about the file rather than a guess.
/// </summary>
public class CorruptArchiveException : Exception
{
    /// <param name="archive">The downloaded archive on disk, which is what the user has to replace.</param>
    /// <param name="location">
    ///     Where the damage was found. Equal to <paramref name="archive" /> unless the failure was inside a
    ///     nested archive, in which case it names the path through the nesting.
    /// </param>
    public CorruptArchiveException(AbsolutePath archive, string location, Exception inner)
        : base(location == archive.ToString() ? $"{archive} is corrupt" : $"{archive} is corrupt, at {location}",
            inner)
    {
        Archive = archive;
        Location = location;
    }

    public AbsolutePath Archive { get; }

    public string Location { get; }
}

/// <summary>
///     Everything that went wrong across one extraction pass.
///
///     Extraction already visits every archive before anything is reported, but only one of the failures
///     used to reach the caller, so a user with several damaged archives was sent round the loop once per
///     file. This carries all of them.
/// </summary>
public class ExtractionFailedException : Exception
{
    public ExtractionFailedException(IReadOnlyList<Exception> failures)
        : base(BuildMessage(failures), failures.Count == 1 ? failures[0] : new AggregateException(failures))
    {
        Failures = failures;
        CorruptArchives = failures.OfType<CorruptArchiveException>().Select(f => f.Archive).ToArray();
    }

    public IReadOnlyList<Exception> Failures { get; }

    /// <summary>
    ///     Archives confirmed corrupt, in the order they failed. Empty when the pass failed for other reasons.
    /// </summary>
    public IReadOnlyList<AbsolutePath> CorruptArchives { get; }

    private static string BuildMessage(IReadOnlyList<Exception> failures)
    {
        var corrupt = failures.OfType<CorruptArchiveException>().Select(f => f.Archive).ToArray();
        var others = failures.Where(f => f is not CorruptArchiveException).ToArray();

        var sb = new StringBuilder();

        if (corrupt.Length > 0)
        {
            sb.Append(corrupt.Length == 1
                ? "1 archive is corrupt and must be deleted, then downloaded again:"
                : $"{corrupt.Length} archives are corrupt and must be deleted, then downloaded again:");

            foreach (var archive in corrupt)
                sb.Append("\n  ").Append(archive);
        }

        if (others.Length > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");

            sb.Append(others.Length == 1
                ? "1 other error occurred while extracting:"
                : $"{others.Length} other errors occurred while extracting:");

            foreach (var other in others)
                sb.Append("\n  ").Append(other.Message);
        }

        return sb.ToString();
    }
}
