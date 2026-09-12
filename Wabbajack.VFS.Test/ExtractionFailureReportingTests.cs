using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     Extraction visits every archive before reporting, but only one failure used to reach the caller, so a
///     user with several damaged archives was told about one, deleted it, ran again, and was told about the
///     next. These cover the shape of the combined report.
/// </summary>
public class ExtractionFailureReportingTests
{
    private static readonly AbsolutePath First = @"C:\Games\downloads\first.7z".ToAbsolutePath();
    private static readonly AbsolutePath Second = @"C:\Games\downloads\second.7z".ToAbsolutePath();
    private static readonly AbsolutePath Third = @"C:\Games\downloads\third.7z".ToAbsolutePath();

    private static CorruptArchiveException Corrupt(AbsolutePath archive, string location = null)
    {
        return new CorruptArchiveException(archive, location ?? archive.ToString(),
            new Exception("Unexpected end of archive"));
    }

    [Fact]
    public void EveryCorruptArchiveIsNamed()
    {
        var ex = new ExtractionFailedException(new List<Exception>
            {Corrupt(First), Corrupt(Second), Corrupt(Third)});

        Assert.Equal(new[] {First, Second, Third}, ex.CorruptArchives);
        Assert.Contains("3 archives are corrupt", ex.Message);
        Assert.Contains(First.ToString(), ex.Message);
        Assert.Contains(Second.ToString(), ex.Message);
        Assert.Contains(Third.ToString(), ex.Message);
    }

    [Fact]
    public void ASingleCorruptArchiveReadsAsOne()
    {
        var ex = new ExtractionFailedException(new List<Exception> {Corrupt(First)});

        Assert.Equal(new[] {First}, ex.CorruptArchives);
        Assert.Contains("1 archive is corrupt", ex.Message);
        Assert.DoesNotContain("archives are corrupt", ex.Message);
    }

    /// <summary>
    ///     A failure that is not corruption must not be swallowed by the corruption summary.
    /// </summary>
    [Fact]
    public void OtherFailuresAreReportedAlongsideCorruption()
    {
        var ex = new ExtractionFailedException(new List<Exception>
        {
            Corrupt(First),
            new InvalidOperationException("disk full")
        });

        Assert.Equal(new[] {First}, ex.CorruptArchives);
        Assert.Contains("1 archive is corrupt", ex.Message);
        Assert.Contains("1 other error occurred", ex.Message);
        Assert.Contains("disk full", ex.Message);
    }

    [Fact]
    public void FailuresWithNoCorruptionStillReport()
    {
        var ex = new ExtractionFailedException(new List<Exception>
        {
            new InvalidOperationException("disk full"),
            new InvalidOperationException("access denied")
        });

        Assert.Empty(ex.CorruptArchives);
        Assert.Contains("2 other errors occurred", ex.Message);
        Assert.Contains("disk full", ex.Message);
        Assert.Contains("access denied", ex.Message);
    }

    /// <summary>
    ///     A single failure keeps its own exception as the inner one, so existing handling that inspects
    ///     InnerException still finds what it expects.
    /// </summary>
    [Fact]
    public void OneFailureIsItsOwnInnerException()
    {
        var corrupt = Corrupt(First);
        var ex = new ExtractionFailedException(new List<Exception> {corrupt});

        Assert.Same(corrupt, ex.InnerException);
    }

    [Fact]
    public void SeveralFailuresAreAllPreserved()
    {
        var failures = new List<Exception> {Corrupt(First), Corrupt(Second)};
        var ex = new ExtractionFailedException(failures);

        var aggregate = Assert.IsType<AggregateException>(ex.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Equal(2, ex.Failures.Count);
    }

    /// <summary>
    ///     Damage found inside a nested archive has to name the downloaded file, since that is what the user
    ///     can actually replace, while still saying where the problem was.
    /// </summary>
    [Fact]
    public void NestedDamageNamesTheDownloadedArchive()
    {
        var nested = Corrupt(First, $"{First}|inner.bsa|textures/rock.dds");

        Assert.Equal(First, nested.Archive);
        Assert.Contains(First.ToString(), nested.Message);
        Assert.Contains("inner.bsa", nested.Message);
    }

    [Fact]
    public void TopLevelDamageDoesNotRepeatTheArchiveName()
    {
        var top = Corrupt(First);

        Assert.Equal($"{First} is corrupt", top.Message);
    }
}
