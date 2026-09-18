using System.Linq;
using System.Text.Json;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     Picking the <c>.ckm</c> to fetch out of a resolved record. The shapes here are the ones the live API
///     returned: a record carrying two slots for the same Creation, one presigned with a bare MD5 and one
///     not, nested at a depth nothing should depend on.
/// </summary>
public class CkmDownloadTests
{
    private const string TwoSlots = """
        {
          "content_id": 5648,
          "title": "Arcane Archer Pack",
          "platform": {
            "files": [
              {
                "download_url": "https://ugcmods.bethesda.net/public/SKYRIM/client/5648/CSV2_5648/Mod.ckm",
                "checksum": "\"d41d8cd98f00b204e9800998ecf8427e-7\"",
                "size": 44040192
              },
              {
                "download_url": "https://ugcmods.bethesda.net/public/SKYRIM/client/5648/CSV2_5648/CSV2_5648.ckm?versionId=abc123",
                "checksum": "5D41402ABC4B2A76B9719D911017C592",
                "size": 44040192
              }
            ]
          }
        }
        """;

    private static JsonElement Record(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public void FindsEverySlotHoweverDeeplyItIsNested()
    {
        Assert.Equal(2, CkmDownload.Slots(Record(TwoSlots)).Count);
    }

    [Fact]
    public void PrefersThePresignedSlot()
    {
        var best = CkmDownload.Best(Record(TwoSlots));

        Assert.NotNull(best);
        Assert.Contains("versionId=abc123", best!.Url.Query);
        Assert.True(best.HasVersionId);
    }

    /// <summary>A checksum is lowercased and unquoted, because neither form is the record's own doing.</summary>
    [Fact]
    public void NormalisesAChecksum()
    {
        var best = CkmDownload.Best(Record(TwoSlots));

        Assert.Equal("5d41402abc4b2a76b9719d911017c592", best!.Checksum);
        Assert.True(best.ChecksumIsMd5);
    }

    /// <summary>
    ///     A multipart etag is a hash of hashes with a part count stuck on the end. Reporting it as a
    ///     checksum would invite a comparison that can only ever fail.
    /// </summary>
    [Fact]
    public void NeverCallsAMultipartEtagAChecksum()
    {
        var etagSlot = CkmDownload.Slots(Record(TwoSlots)).Single(s => !s.HasVersionId);

        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e-7", etagSlot.Checksum);
        Assert.False(etagSlot.ChecksumIsMd5);
    }

    [Fact]
    public void ReadsTheContentIdOutOfTheUrlItself()
    {
        var best = CkmDownload.Best(Record(TwoSlots));

        Assert.Equal(5648, best!.ContentId);
        Assert.Equal("Arcane Archer Pack", best.Title);
        Assert.Equal(44040192, best.Size);
    }

    /// <summary>
    ///     A record with no <c>content_id</c> of its own still resolves, because the URL path carries it.
    ///     The two disagreeing is the URL's to settle: it is what will actually be fetched.
    /// </summary>
    [Fact]
    public void FallsBackToTheUrlWhenTheRecordDoesNotSayWhichCreationItIs()
    {
        var best = CkmDownload.Best(Record("""
            {
              "download_url": "https://ugcmods.bethesda.net/public/SKYRIM/client/5695/CSV2_5695/CSV2_5695.ckm?versionId=z"
            }
            """));

        Assert.Equal(5695, best!.ContentId);
        Assert.Null(best.Checksum);
        Assert.Null(best.Size);
    }

    [Theory]
    [InlineData("""{"content_id": 5648, "title": "No download at all"}""")]
    [InlineData("""{"download_url": "https://ugcmods.bethesda.net/public/SKYRIM/client/5648/preview.jpg"}""")]
    [InlineData("""{"download_url": ""}""")]
    [InlineData("""{"download_url": "not a url at all.ckm"}""")]
    [InlineData("""{"download_url": 5648}""")]
    public void FindsNothingToFetchInARecordThatOffersNothing(string json)
    {
        Assert.Null(CkmDownload.Best(Record(json)));
    }

    /// <summary>
    ///     The whole response at once: one slot per Creation, ordered by id, with the presigned one winning
    ///     even when it is not the first the walk meets.
    /// </summary>
    [Fact]
    public void ReducesAWholeResponseToOneSlotPerCreation()
    {
        var response = JsonDocument.Parse($"""[{TwoSlots}, {TwoSlots}, {SingleSlot(5615)}]""");

        var slots = CkmDownload.BestPerContent(response.RootElement.EnumerateArray());

        Assert.Equal(new long[] {5615, 5648}, slots.Select(s => s.ContentId));
        Assert.All(slots, slot => Assert.True(slot.HasVersionId));
    }

    private static string SingleSlot(long contentId)
    {
        return $$"""
            {
              "content_id": {{contentId}},
              "download_url": "https://ugcmods.bethesda.net/public/SKYRIM/client/{{contentId}}/CSV2_{{contentId}}/CSV2_{{contentId}}.ckm?versionId=q"
            }
            """;
    }
}
