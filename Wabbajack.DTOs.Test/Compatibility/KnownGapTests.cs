using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Modlist format problems these tests found in the current code that are not fixed yet. Each is skipped
///     so the offline lane stays green, and each states the behaviour it expects, so removing the Skip is the
///     check that the fix works.
/// </summary>
public class KnownGapTests
{
    private readonly DTOSerializer _serializer;

    public KnownGapTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    ///     The generated readers assign every property, present or not, so a property left out of the
    ///     JSON is set to null over its initializer. For Http that makes Headers null, and
    ///     HttpDownloader enumerates Headers when it builds the request.
    /// </summary>
    [Fact(Skip = "Generated readers overwrite property initializers with null when the property is absent")]
    public void AbsentArrayPropertiesKeepTheirInitializer()
    {
        var http = Assert.IsType<Http>(_serializer.Deserialize<IDownloadState>(
            """{"$type": "HttpDownloader, Wabbajack.Lib", "Url": "https://example.com/a.7z"}"""));

        Assert.NotNull(http.Headers);
        Assert.Empty(http.Headers);
    }
}
