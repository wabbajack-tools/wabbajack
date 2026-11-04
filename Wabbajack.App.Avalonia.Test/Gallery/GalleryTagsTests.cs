using System;
using System.Collections.Generic;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Gallery;

/// <summary>
///     The tags a gallery tile shows. <c>tag_mappings.json</c> maps the names authors use onto the allowed ones
///     ("Requiem" is "Hardcore"); the mapping used to be looked up and then overwritten, so a list tagged with a
///     mapped name lost the tag and never turned up under that tag's filter.
/// </summary>
public class GalleryTagsTests
{
    private static readonly HashSet<string> Allowed =
        new(new[] {"Hardcore", "Vanilla+", "DLC Required", "VR"}, StringComparer.OrdinalIgnoreCase);

    // From the live tag_mappings.json.
    private static readonly Dictionary<string, string> Mappings = new()
    {
        ["Requiem"] = "Hardcore", ["Vanilla Plus"] = "Vanilla+", ["Lite"] = "Vanilla+", ["AE DLC"] = "DLC Required"
    };

    private static List<string> Normalize(params string[] raw) =>
        GalleryTags.Normalize(raw, Allowed, Mappings, false, false, false);

    [Fact]
    public void MappedTag_IsShownUnderItsAllowedName()
    {
        Assert.Equal(new[] {"Hardcore", "DLC Required"}, Normalize("Requiem", "AE DLC"));
    }

    [Fact]
    public void AllowedTag_KeepsItsAllowedSpelling()
    {
        Assert.Equal(new[] {"VR", "Hardcore"}, Normalize("vr", "HARDCORE"));
    }

    [Fact]
    public void TagThatIsNeitherAllowedNorMapped_IsDropped()
    {
        Assert.Equal(new[] {"Hardcore"}, Normalize("Hardcore", "Made Up Tag"));
    }

    [Fact]
    public void TwoNamesForOneTag_ShowItOnce()
    {
        Assert.Equal(new[] {"Vanilla+"}, Normalize("Lite", "Vanilla Plus", "Vanilla+"));
    }

    [Fact]
    public void GalleryOwnTags_ComeFirst()
    {
        Assert.Equal(new[] {"Unavailable", "Featured", "NSFW", "Hardcore"},
            GalleryTags.Normalize(new[] {"Requiem"}, Allowed, Mappings, nsfw: true, official: true, unavailable: true));
    }
}
