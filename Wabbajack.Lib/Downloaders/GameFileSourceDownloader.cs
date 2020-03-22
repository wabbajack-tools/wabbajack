using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Lib.Downloaders
{
    public class GameFileSourceDownloader : IDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var gameName = (string)archiveINI?.General?.gameName;
            var gameFile = (string)archiveINI?.General?.gameFile;

            if (gameFile == null || gameFile == null)
                return null;

            var game = GameRegistry.GetByFuzzyName(gameName);
            if (game == null) return null;

            var path = game.GameLocation();
            var filePath = Path.Combine(path, gameFile);
            
            if (!File.Exists(filePath))
                return null;

            var hash = filePath.FileHashCached();

            return new State
            {
                Game = game.Game, 
                GameFile = gameFile,
                Hash = hash,
                GameVersion = game.InstalledVersion
            };
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public Game Game { get; set; }
            public string GameFile { get; set; }
            public Hash Hash { get; set; }
            
            public string GameVersion { get; set; }

            internal string SourcePath => Path.Combine(Game.MetaData().GameLocation(), GameFile);

            public override object[] PrimaryKey { get => new object[] {Game, GameVersion, GameFile}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                using(var src = File.OpenRead(SourcePath))
                using (var dest = File.Open(destination, System.IO.FileMode.Create))
                {
                    var size = new FileInfo(SourcePath).Length;
                    src.CopyToWithStatus(size, dest, "Copying from Game folder");
                }
                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                return File.Exists(SourcePath) && SourcePath.FileHashCached() == Hash;
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
