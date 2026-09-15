using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("Nexus")]
[JsonName("NexusDownloader, Wabbajack.Lib")]
public class Nexus : ADownloadState, IMetaState
{
    [JsonPropertyName("GameName")] public Game Game { get; set; }
    public long ModID { get; set; }
    public long FileID { get; set; }

    public override string TypeName => "NexusDownloader+State";
    public override object[] PrimaryKey => new object[] {Game, ModID, FileID};
    public string? Name { get; set; }

    public string? Author { get; set; }

    public string? Version { get; set; }

    public Uri? ImageURL { get; set; }

    public bool IsNSFW { get; set; }

    public string? Description { get; set; }
    /// <summary>
    ///     The mod's page, which is what both readers of this want: the archive grid opens it to show the
    ///     user a mod, and the CLI's changelog writes it into a manifest. The file's own page is a different
    ///     thing and lives in <c>ManualDownloadUrls</c>. Shares <see cref="GameMetaData.NexusDomain" /> with
    ///     it so the two cannot disagree about a game with no recorded Nexus address.
    /// </summary>
    public Uri? LinkUrl => new($"https://www.nexusmods.com/{Game.MetaData().NexusDomain}/mods/{ModID}");

    public Task<bool> LoadMetaData()
    {
        return Task.FromResult(false);
    }
}