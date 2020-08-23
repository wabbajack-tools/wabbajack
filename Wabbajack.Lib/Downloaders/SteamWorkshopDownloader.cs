using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Common.StoreHandlers;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class SteamWorkshopDownloader : IUrlDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var id = archiveINI?.General?.itemID;
            if (id == null) return null;
            var steamID = archiveINI?.General?.steamID;
            if (steamID == null)
            {
                Utils.Error($"Steam Workshop Item {id} has no Steam Game ID!");
                return null;
            }

            var game = StoreHandler.Instance.SteamHandler.Games.FirstOrDefault(x => x.ID == int.Parse(steamID));
            if (game == null)
            {
                Utils.Error($"Unable to find Steam Game {steamID}");
                return null;
            }
            var item = new SteamWorkshopItem((SteamGame)game)
            {
                ItemID = int.Parse(id)
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

        [JsonName(("SteamWorkshopDownloader"))]
        public class State : AbstractDownloadState
        {
            public SteamWorkshopItem Item { get; }

            public override object[] PrimaryKey => new object[] { Item.SteamGameID, Item.ItemID };

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
                if (Item.SteamGame == null)
                {
                    var game = StoreHandler.Instance.SteamHandler.Games.FirstOrDefault(x => x.ID == Item.SteamGameID);
                    if(game == null)
                        return false;
                    Item.SteamGame = (SteamGame)game;
                }
                var currentLib = Item.SteamGame.Universe;

                var downloadFolder = new RelativePath($"workshop//downloads//{Item.SteamGame.ID}").RelativeTo(currentLib);
                var contentFolder = new RelativePath($"workshop//content//{Item.SteamGame.ID}").RelativeTo(currentLib);
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = new RelativePath("steam.exe").RelativeTo(StoreHandler.Instance.SteamHandler.SteamPath).ToString(),
                        CreateNoWindow = true,
                        Arguments = $"console +workshop_download_item {Item.SteamGame.ID} {Item.ItemID}"
                    }
                };

                p.Start();
                
                var finished = false;
                var itemDownloadPath = new RelativePath(Item.ItemID.ToString()).RelativeTo(downloadFolder);
                var itemContentPath = new RelativePath(Item.ItemID.ToString()).RelativeTo(contentFolder);

                await Task.Run(() =>
                {
                    while (!finished)
                    {
                        if (!itemDownloadPath.Exists)
                            if (itemContentPath.Exists)
                                finished = true;

                        if (finished) break;
                        Thread.Sleep(1000);
                    }
                });
                
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
                    $"steamID={Item.SteamGameID}",
                    $"itemSize={Item.Size}"
                };
            }
        }
    }
}
