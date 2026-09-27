using System;
using System.Linq;
using Wabbajack.DTOs;

namespace Wabbajack.Compiler;

public static class GalleryEntry
{
    public const string ImagePlaceholder =
        "REPLACE THIS WITH A RAW LINK TO A 16:9 IMAGE HOSTED ON YOUR REPOSITORY, AT OR BELOW 1MB, IN WEBP, PNG OR JPG FORMAT";

    public const string DownloadPlaceholder =
        "REPLACE THIS WITH THE CDN LINK OF YOUR UPLOADED WABBAJACK FILE";

    public const string ReadmePlaceholder =
        "REPLACE THIS WITH A LINK TO YOUR INSTALLATION DOCUMENTATION";

    public const string MaintainerPlaceholder =
        "github/REPLACE THIS WITH YOUR GITHUB USERNAME";

    public static ModlistMetadata Build(CompilerSettings settings, DownloadMetadata downloadMetadata,
        string? githubUsername)
    {
        var now = DateTime.UtcNow;
        return new ModlistMetadata
        {
            Title = settings.ModListName,
            Description = settings.ModListDescription,
            Author = settings.ModListAuthor,
            Maintainers = new[] {githubUsername != null ? $"github/{githubUsername}" : MaintainerPlaceholder},
            Game = settings.Game,
            Tags = settings.ModListTags?.ToList() ?? new(),
            NSFW = settings.ModlistIsNSFW,
            UtilityList = settings.ModlistIsUtilityList,
            Links = new LinksObject
            {
                ImageUri = ImagePlaceholder,
                Readme = string.IsNullOrWhiteSpace(settings.ModListReadme) ? ReadmePlaceholder : settings.ModListReadme,
                Download = DownloadPlaceholder,
                MachineURL = MachineUrl(settings),
                DiscordURL = settings.ModListCommunity,
                WebsiteURL = settings.ModListWebsite
            },
            DownloadMetadata = downloadMetadata,
            Version = settings.Version,
            DateCreated = now,
            DateUpdated = now
        };
    }

    public static string MachineUrl(CompilerSettings settings)
    {
        var url = settings.MachineUrl ?? "";
        var slash = url.LastIndexOf('/');
        if (slash >= 0) url = url[(slash + 1)..];
        if (!string.IsNullOrWhiteSpace(url)) return url;

        var words = (settings.ModListName ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => new string(w.Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-').ToArray()))
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Concat(words);
    }
}
