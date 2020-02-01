using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class SteamWorkshopDownloader : IUrlDownloader
    {
        private SteamWorkshopItem _item;

        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
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

        public async Task Prepare()
        {
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            throw new NotImplementedException();
        }

        public class State : AbstractDownloadState
        {
            public override string URL { get; set; }

            public SteamWorkshopItem Item { get; set; }
            public override object[] PrimaryKey { get => new object[] {Item.Game, Item.ItemID}; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                var currentLib = Item.Game.Universe;

                var downloadFolder = Path.Combine(currentLib, "workshop", "downloads", Item.Game.ID.ToString());
                var contentFolder = Path.Combine(currentLib, "workshop", "content", Item.Game.ID.ToString());
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(StoreHandler.Instance.SteamHandler.SteamPath, "steam.exe"),
                        CreateNoWindow = true,
                        Arguments = $"console +workshop_download_item {Item.Game.ID} {Item.ItemID}"
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

                return true;
            }

            public override async Task<bool> Verify(Archive a)
            {
                //TODO: find a way to verify steam workshop items
                throw new NotImplementedException();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<SteamWorkshopDownloader>();
            }

            public override string[] GetMetaIni()
            {
                return new[]
                {
                    "[General]", 
                    $"itemID={Item.ItemID}", 
                    $"steamID={Item.Game.Game.MetaData().SteamIDs.First()}",
                    $"itemSize={Item.Size}"
                };
            }
        }
    }
}
