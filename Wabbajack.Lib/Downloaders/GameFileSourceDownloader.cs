using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MessagePack;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Lib.Downloaders
{
    public class GameFileSourceDownloader : IDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var gameName = (string)archiveINI?.General?.gameName;
            var gameFile = (string)archiveINI?.General?.gameFile;

            if (gameFile == null || gameFile == null)
                return null;

            var game = GameRegistry.GetByFuzzyName(gameName);
            if (game == null) return null;

            var path = game.GameLocation();
            var filePath = path?.Combine(gameFile);
            
            
            if (!filePath?.Exists ?? false)
                return null;

            var fp = filePath.Value;
            var hash = await fp.FileHashCachedAsync();

            return new State
            {
                Game = game.Game, 
                GameFile = (RelativePath)gameFile,
                Hash = hash,
                GameVersion = game.InstalledVersion
            };
        }

        public async Task Prepare()
        {
        }

        [MessagePackObject]
        public class State : AbstractDownloadState
        {
            [Key(0)]
            public Game Game { get; set; }
            [Key(1)]
            public RelativePath GameFile { get; set; }
            [Key(2)]
            public Hash Hash { get; set; }
            [Key(3)]
            public string GameVersion { get; set; }

            [IgnoreMember]
            internal AbsolutePath SourcePath => Game.MetaData().GameLocation().Value.Combine(GameFile);

            [IgnoreMember]
            public override object[] PrimaryKey { get => new object[] {Game, GameVersion, GameFile}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                await using var src = SourcePath.OpenRead();
                await using var dest = destination.Create();
                var size = SourcePath.Size;
                await src.CopyToWithStatusAsync(size, dest, "Copying from Game folder");

                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                return SourcePath.Exists && await SourcePath.FileHashCachedAsync() == Hash;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<GameFileSourceDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return null;
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", $"gameName={Game.MetaData().MO2ArchiveName}", $"gameFile={GameFile}"};
            }
        }
    }
}
