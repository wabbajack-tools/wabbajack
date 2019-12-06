using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class SteamWorkshopDownloader : IUrlDownloader
    {
        private SteamWorkshopItem _item;

        public AbstractDownloadState GetDownloaderState(dynamic archiveINI)
        {
            var id = archiveINI?.General?.itemID;
            var steamID = archiveINI?.General?.steamID;
            var size = archiveINI?.General?.itemSize;
            _item = new SteamWorkshopItem
            {
                ItemID = id != null ? int.Parse(id) : 0,
                Size = size != null ? int.Parse(size) : 0,
                Game = steamID != null ? GameRegistry.GetBySteamID(int.Parse(steamID)) : null
            };
            return new State {Item = _item};
        }

        public void Prepare()
        {
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            throw new NotImplementedException();
        }

        public class State : AbstractDownloadState
        {
            public SteamWorkshopItem Item { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task Download(Archive a, string destination)
            {
                var currentLib = "";
                SteamHandler.Instance.InstallFolders.Where(f => f.Contains(Item.Game.InstallDir)).Do(s => currentLib = s);

                var downloadFolder = Path.Combine(currentLib, "workshop", "downloads", Item.Game.AppId.ToString());
                var contentFolder = Path.Combine(currentLib, "workshop", "content", Item.Game.AppId.ToString());
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(SteamHandler.Instance.SteamPath, "steam.exe"),
                        CreateNoWindow = true,
                        Arguments = $"console +workshop_download_item {Item.Game.AppId} {Item.ItemID}"
                    }
                };

                p.Start();

                //TODO: async
                var finished = false;
                while (!finished)
                {
                    if(!Directory.Exists(Path.Combine(downloadFolder, Item.ItemID.ToString())))
                        if (Directory.Exists(Path.Combine(contentFolder, Item.ItemID.ToString())))
                            finished = true;

                    Thread.Sleep(1000);
                }
            }

            public override async Task<bool> Verify()
            {
                //TODO: find a way to verify steam workshop items
                throw new NotImplementedException();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<SteamWorkshopDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* Steam - [{Item.ItemID}]";
            }
        }
    }
}
