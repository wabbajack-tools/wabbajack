using System;
using Wabbajack.FileExtractor.ExtractorHelpers;
using Xunit;

namespace Wabbajack.FileExtractor.Test.Offline;

/// <summary>
///     innoextract's <c>--list-sizes</c> lines are what the extractor sizes its progress by, read from the
///     bracketed size at the end of each line.
/// </summary>
public class InnoHelperTests
{
    [Theory]
    [InlineData(" - \"app/readme.txt\" (12 B)", 12)]
    [InlineData(" - \"app/data.bin\" (1.5 KiB)", 1536)]
    [InlineData(" - \"app/(copy) data.bin\" (2 MiB)", 2 * 1024 * 1024)]
    [InlineData(" - \"app/archive.bsa\" (1 GiB)", 1024 * 1024 * 1024)]
    [InlineData(" - \"app/ten.bin\" (10.25 KiB) [temp]", 10496)]
    public void ReadsTheSizeAtTheEndOfTheLine(string line, int expected)
    {
        Assert.Equal(expected, InnoHelper.GetExtractedFileSize(line));
    }

    [Fact]
    public void UnsupportedUnit_IsRefused()
    {
        Assert.Throws<NotSupportedException>(() => InnoHelper.GetExtractedFileSize(" - \"huge.bin\" (2 TiB)"));
    }
}
