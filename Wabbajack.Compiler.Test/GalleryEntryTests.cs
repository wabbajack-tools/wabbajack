using System;
using System.Linq;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class GalleryEntryTests
{
    private readonly DTOSerializer _dtos;

    public GalleryEntryTests(DTOSerializer dtos)
    {
        _dtos = dtos;
    }

    [Theory]
    [InlineData("JohnsLists/JohnsSkyrimMakeover", "", "JohnsSkyrimMakeover")]
    [InlineData("JohnsSkyrimMakeover", "", "JohnsSkyrimMakeover")]
    [InlineData("", "John's Skyrim Makeover", "JohnsSkyrimMakeover")]
    [InlineData("", "Halgari's Helper 2.0", "HalgarisHelper20")]
    [InlineData("", "Fallout 4 - Rebuilt", "Fallout4-Rebuilt")]
    [InlineData("JohnsLists/", "John's Skyrim Makeover", "JohnsSkyrimMakeover")]
    [InlineData("", "", "")]
    [InlineData(null, null, "")]
    public void MachineUrlIsUnNamespacedOrDerivedFromTheTitle(string machineUrl, string title, string expected)
    {
        var settings = new CompilerSettings {MachineUrl = machineUrl, ModListName = title};
        Assert.Equal(expected, GalleryEntry.MachineUrl(settings));
    }

    private static CompilerSettings FullSettings => new()
    {
        ModListName = "Johns Skyrim Makeover",
        ModListAuthor = "JohnDoe",
        ModListDescription = "A lightweight visual makeover",
        ModListReadme = "https://github.com/JohnDoe/lists/blob/main/README.md",
        ModListWebsite = "https://johndoe.github.io/lists/",
        ModListCommunity = "https://discord.gg/example",
        MachineUrl = "JohnsLists/JohnsSkyrimMakeover",
        Game = Game.SkyrimSpecialEdition,
        ModlistIsNSFW = true,
        ModlistIsUtilityList = true,
        ModListTags = new[] {"SFW", "Vanilla+"},
        Version = Version.Parse("1.2.3")
    };

    private static DownloadMetadata Metadata => new()
    {
        Hash = Hash.FromLong(42),
        Size = 1234,
        NumberOfArchives = 5,
        SizeOfArchives = 5678,
        NumberOfInstalledFiles = 90,
        SizeOfInstalledFiles = 9876
    };

    [Fact]
    public void EverythingTheCompilerKnowsIsFilledIn()
    {
        var entry = GalleryEntry.Build(FullSettings, Metadata, "JohnDoe");

        Assert.Equal("Johns Skyrim Makeover", entry.Title);
        Assert.Equal("JohnDoe", entry.Author);
        Assert.Equal("A lightweight visual makeover", entry.Description);
        Assert.Equal(new[] {"github/JohnDoe"}, entry.Maintainers);
        Assert.Equal(Game.SkyrimSpecialEdition, entry.Game);
        Assert.Equal(new[] {"SFW", "Vanilla+"}, entry.Tags);
        Assert.True(entry.NSFW);
        Assert.True(entry.UtilityList);
        Assert.False(entry.ForceDown);
        Assert.Equal("JohnsSkyrimMakeover", entry.Links.MachineURL);
        Assert.Equal("https://github.com/JohnDoe/lists/blob/main/README.md", entry.Links.Readme);
        Assert.Equal("https://johndoe.github.io/lists/", entry.Links.WebsiteURL);
        Assert.Equal("https://discord.gg/example", entry.Links.DiscordURL);
        Assert.Equal(Version.Parse("1.2.3"), entry.Version);
        Assert.NotNull(entry.DownloadMetadata);
        Assert.Equal(1234, entry.DownloadMetadata!.Size);
        Assert.Equal(Hash.FromLong(42), entry.DownloadMetadata.Hash);
    }

    [Fact]
    public void WhatTheCompilerCannotKnowIsAnObviousPlaceholder()
    {
        var settings = FullSettings;
        settings.ModListReadme = "";
        var entry = GalleryEntry.Build(settings, Metadata, null);

        Assert.Equal(new[] {GalleryEntry.MaintainerPlaceholder}, entry.Maintainers);
        Assert.Equal(GalleryEntry.ImagePlaceholder, entry.Links.ImageUri);
        Assert.Equal(GalleryEntry.DownloadPlaceholder, entry.Links.Download);
        Assert.Equal(GalleryEntry.ReadmePlaceholder, entry.Links.Readme);

        Assert.False(Uri.TryCreate(entry.Links.ImageUri, UriKind.Absolute, out _));
        Assert.False(Uri.TryCreate(entry.Links.Download, UriKind.Absolute, out _));
    }

    [Fact]
    public void TheEmittedJsonRoundTripsThroughTheGalleryDeserializer()
    {
        var entry = GalleryEntry.Build(FullSettings, Metadata, "JohnDoe");
        var json = _dtos.Serialize(new[] {entry}, writeIndented: true);

        var roundTripped = _dtos.Deserialize<ModlistMetadata[]>(json);

        Assert.NotNull(roundTripped);
        var back = Assert.Single(roundTripped!);
        Assert.Equal(entry.Title, back.Title);
        Assert.Equal(entry.Maintainers, back.Maintainers);
        Assert.Equal(entry.Game, back.Game);
        Assert.Equal(entry.Tags, back.Tags);
        Assert.Equal(entry.Links.MachineURL, back.Links.MachineURL);
        Assert.Equal(entry.Version, back.Version);
        Assert.Equal(entry.DownloadMetadata!.Hash, back.DownloadMetadata!.Hash);
        Assert.Equal(entry.DownloadMetadata.Size, back.DownloadMetadata.Size);
        Assert.Contains("\"1.2.3\"", json);
    }
}
