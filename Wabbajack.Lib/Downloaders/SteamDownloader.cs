using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using Microsoft.Win32;
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

                string SteamDirectory = (string)Registry.CurrentUser.OpenSubKey(@"Software\\Valve\\Steam", false).GetValue("SteamPath");
                string SteamWorkshopId = HttpUtility.ParseQueryString(new Uri(Url).Query).Get("id");
                Console.WriteLine(SteamWorkshopId);
                if (Directory.Exists(SteamDirectory))
                {
                    using (Process SteamConsole = new Process())
                    {
                        SteamConsole.StartInfo.FileName = Path.Combine(SteamDirectory, "Steam.exe");
                        SteamConsole.StartInfo.CreateNoWindow = true; // set to true after debug?
                                                                      // HARD CODED SKYRIM LEGENDARY EDITION
                                                                      // To do: get Steam game ID
                        SteamConsole.StartInfo.Arguments = "console " + "+workshop_download_item " + "72850" + " " + SteamWorkshopId;
                        SteamConsole.Start();
                    }
                    string SteamModContentFolder = Path.Combine(SteamDirectory, "steamapps", "workshop", "content", "72850", SteamWorkshopId);
                    string SteamModDownloadFolder = Path.Combine(SteamDirectory, "steamapps", "workshop", "downloads", "72850", SteamWorkshopId);
                    while(Directory.GetFiles(SteamModDownloadFolder).Length > 0)
                    {
                    }
                    Directory.Move(SteamModDownloadFolder, destination);
                }
            }
        }
    }
}
