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

            var game = GameRegistry.GetByMO2ArchiveName(gameName);
            if (game == null) return null;

            var path = game.GameLocation();
            var filePath = Path.Combine(path, gameFile);
            
            if (!File.Exists(filePath))
                return null;

            var hash = filePath.FileHashCached();

            return new State
            {
                Game = GameRegistry.GetByMO2ArchiveName(gameName).Game, 
                GameFile = gameFile,
                Hash = hash,
                GameVersion = GameRegistry.GetByMO2ArchiveName(gameName).InstalledVersion
            };
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public Game Game { get; set; }
            public string GameFile { get; set; }
            public string Hash { get; set; }
            
            public string GameVersion { get; set; }

            internal string SourcePath => Path.Combine(Game.MetaData().GameLocation(), GameFile);

            public override object[] PrimaryKey { get => new object[] {Game, GameVersion, GameFile}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task Download(Archive a, string destination)
            {
                using(var src = File.OpenRead(SourcePath))
                using (var dest = File.OpenWrite(destination))
                {
                    var size = new FileInfo(SourcePath).Length;
                    src.CopyToWithStatus(size, dest, "Copying from Game folder");
                }
            }

            public override async Task<bool> Verify()
            {
                return File.Exists(SourcePath) && SourcePath.FileHashCached() == Hash;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<GameFileSourceDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* Game File {Game} - {GameFile}";
            }
        }
    }
}
