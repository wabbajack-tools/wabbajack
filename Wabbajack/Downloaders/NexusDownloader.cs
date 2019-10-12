using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.NexusApi;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
{
    public class NexusDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var general = archive_ini?.General;

            if (general.modID != null && general.fileID != null && general.gameName != null)
            {
                var info = new NexusApiClient().GetModInfo(general.gameName, general.modID);
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
                    NexusURL = NexusApiUtils.GetModURL(info.game_name, info.mod_id),
                    Summary = info.summary,
                    Adult = info.contains_adult_content

                };
            }

            return null;
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
                    new NexusApiClient().GetNexusDownloadLink(this, true);
                    return true;
                }
                catch (Exception ex)
                {
                    Utils.Log($"{ModName} - {GameName} - {ModID} - {FileID} - Error Getting Nexus Download URL - {ex.Message}");
                    return false;
                }

            }
        }
    }
}
