using System;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class NexusDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archiveINI)
        {
            var general = archiveINI?.General;

            if (general.modID != null && general.fileID != null && general.gameName != null)
            {
                var name = (string)general.gameName;
                var gameMeta = GameRegistry.GetByMO2ArchiveName(name);
                var game = gameMeta != null ? GameRegistry.GetByMO2ArchiveName(name).Game : GameRegistry.GetByNexusName(name).Game;
                var info = new NexusApiClient().GetModInfo(game, general.modID);
                return new State
                {
                    GameName = general.gameName,
                    FileID = general.fileID,
                    ModID = general.modID,
                    Version = general.version ?? "0.0.0.0",
                    Author = info.author,
                    UploadedBy = info.uploaded_by,
                    UploaderProfile = info.uploaded_users_profile_url,
                    ModName = info.name,
                    SlideShowPic = info.picture_url,
                    NexusURL = NexusApiUtils.GetModURL(game, info.mod_id),
                    Summary = info.summary,
                    Adult = info.contains_adult_content

                };
            }

            return null;
        }

        public void Prepare()
        {
            var client = new NexusApiClient();
            var status = client.GetUserStatus();
            if (!client.IsAuthenticated)
            {
                Utils.Error($"Authenticating for the Nexus failed. A nexus account is required to automatically download mods.");
                return;
            }

            if (status.is_premium) return;
            Utils.Error($"Automated installs with Wabbajack requires a premium nexus account. {client.Username} is not a premium account.");
        }

        public class State : AbstractDownloadState
        {
            public string Author;
            public string FileID;
            public string GameName;
            public string ModID;
            public string UploadedBy;
            public string UploaderProfile;
            public string Version;
            public string SlideShowPic;
            public string ModName;
            public string NexusURL;
            public string Summary;
            public bool Adult;

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                // Nexus files are always whitelisted
                return true;
            }

            public override void Download(Archive a, string destination)
            {
                string url;
                try
                {
                    url = new NexusApiClient().GetNexusDownloadLink(this, false);
                }
                catch (Exception ex)
                {
                    Utils.Log($"{a.Name} - Error Getting Nexus Download URL - {ex.Message}");
                    return;
                }

                Utils.Log($"Downloading Nexus Archive - {a.Name} - {GameName} - {ModID} - {FileID}");

                new HTTPDownloader.State
                {
                    Url = url
                }.Download(a, destination);

            }

            public override bool Verify()
            {
                try
                {
                    var modfiles = new NexusApiClient().GetModFiles(GameRegistry.GetByMO2ArchiveName(GameName).Game, int.Parse(ModID));
                    var fileid = ulong.Parse(FileID);
                    var found = modfiles.files
                        .FirstOrDefault(file => file.file_id == fileid && file.category_name != null);
                    return found != null;
                }
                catch (Exception ex)
                {
                    Utils.Log($"{ModName} - {GameName} - {ModID} - {FileID} - Error Getting Nexus Download URL - {ex}");
                    return false;
                }

            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<NexusDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                var profile = UploaderProfile.Replace("/games/",
                    "/" + NexusApiUtils.ConvertGameName(GameName).ToLower() + "/");

                return string.Join("\n", 
                    $"* [{a.Name}](http://nexusmods.com/{NexusApiUtils.ConvertGameName(GameName)}/mods/{ModID})", 
                    $"    * Author : [{UploadedBy}]({profile})", 
                    $"    * Version : {Version}");
            }
        }
    }
}
