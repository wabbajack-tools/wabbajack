using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class SteamWorkshopDownloader : IUrlDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var id = archiveINI?.General?.itemID;
            var steamID = archiveINI?.General?.steamID;
            var size = archiveINI?.General?.itemSize;
            if (steamID == null)
            {
                throw new ArgumentException("Steam workshop item had no steam ID.");
            }
            var item = new SteamWorkshopItem(GameRegistry.GetBySteamID(int.Parse(steamID)))
            {
                ItemID = id != null ? int.Parse(id) : 0,
                Size = size != null ? int.Parse(size) : 0,
            };
            return new State(item);
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
            public SteamWorkshopItem Item { get; }

            public override object[] PrimaryKey => new object[] { Item.Game, Item.ItemID };

            public State(SteamWorkshopItem item)
            {
                Item = item;
            }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                var currentLib = Item.Game.Universe;

                var downloadFolder = new RelativePath($"workshop//downloads//{Item.Game.ID}").RelativeTo(currentLib);
                var contentFolder = new RelativePath($"workshop//content//{Item.Game.ID}").RelativeTo(currentLib);
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = new RelativePath("steam.exe").RelativeTo(StoreHandler.Instance.SteamHandler.SteamPath).ToString(),
                        CreateNoWindow = true,
                        Arguments = $"console +workshop_download_item {Item.Game.ID} {Item.ItemID}"
                    }
                };

                p.Start();

                //TODO: async
                var finished = false;
                var itemDownloadPath = new RelativePath(Item.ItemID.ToString()).RelativeTo(downloadFolder);
                var itemContentPath = new RelativePath(Item.ItemID.ToString()).RelativeTo(contentFolder);
                while (!finished)
                {
                    if(!itemDownloadPath.Exists)
                        if(itemContentPath.Exists)
                            finished = true;

                    Thread.Sleep(1000);
                }

                return true;
            }

            public override async Task<bool> Verify(Archive a, CancellationToken? token)
            {
                //TODO: find a way to verify steam workshop items
                throw new NotImplementedException();
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<SteamWorkshopDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return $"https://steamcommunity.com/sharedfiles/filedetails/?id={Item.ItemID}";
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
