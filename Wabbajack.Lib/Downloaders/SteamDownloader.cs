using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class SteamDownloader : IDownloader, IUrlDownloader
    {

        public AbstractDownloadState GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url != null && url.StartsWith(Consts.SteamPrefix))
                return new State { Url = url };
            return null;
        }

        public void Prepare()
        {
        }

        public class State : HTTPDownloader.State
        {
            public override void Download(Archive a, string destination)
            {
                SteamHandler SHandler = new SteamHandler(true);
                string SteamDirectory = SHandler.SteamPath;
                string SteamWorkshopId = HttpUtility.ParseQueryString(new Uri(Url).Query).Get("id");
                if (Directory.Exists(SteamDirectory))
                {
                    using (Process SteamConsole = new Process())
                    {
                        // Todo: replace 72850 (Skyrim LE) with game ID that's being installed.
                        SteamConsole.StartInfo.FileName = Path.Combine(SteamDirectory, "Steam.exe");
                        SteamConsole.StartInfo.CreateNoWindow = true;
                        SteamConsole.StartInfo.Arguments = "console " + "+workshop_download_item " + "72850" + " " + SteamWorkshopId;
                        SteamConsole.Start();
                        Utils.Log($"Starting download of Steam Workshop item {SteamWorkshopId}");
                    }
                    string SteamModContentFolder = Path.Combine(SteamDirectory, "steamapps", "workshop", "content", "72850", SteamWorkshopId);
                    string SteamModDownloadFolder = Path.Combine(SteamDirectory, "steamapps", "workshop", "downloads", "72850", SteamWorkshopId);

                    // Not sure on how to do this in the most optimal way. Checking every second if the downloads are done yet, after which they are moved.
                        while (Directory.Exists(SteamModDownloadFolder)) { System.Threading.Thread.Sleep(1000); }

                    Utils.Log($"Moving Steam Workshop item {SteamWorkshopId} to {destination}");
                    Directory.Move(SteamModContentFolder, destination);
                }
            }
        }
    }
}
