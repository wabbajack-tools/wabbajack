using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.DownloadStates;

[JsonAlias("GameFileSource")]
[JsonName("GameFileSourceDownloader, Wabbajack.Lib")]
public class GameFileSource : ADownloadState
{
    public Game Game { get; set; }
    public RelativePath GameFile { get; set; }
    public Hash Hash { get; set; }
    public string GameVersion { get; set; } = "";
    public override string TypeName => "GameFileSourceDownloader+State";

    public override object[] PrimaryKey => new object[]
        {Game, GameVersion ?? "0.0.0.0", GameFile.ToString().ToLowerInvariant()};
}