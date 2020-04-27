using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Lib.ModListRegistry
{
    [JsonName("ModListMetadata")]
    public class ModlistMetadata
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("game")]
        public Game Game { get; set; }

        [JsonIgnore] public string GameName => Game.ToDescriptionString();

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("nsfw")]
        public bool NSFW { get; set; }

        [JsonProperty("links")]
        public LinksObject Links { get; set; } = new LinksObject();

        [JsonProperty("download_metadata")]
        public DownloadMetadata? DownloadMetadata { get; set; }

        [JsonIgnore] 
        public ModListSummary ValidationSummary { get; set; } = new ModListSummary();

        [JsonName("Links")]
        public class LinksObject
        {
            [JsonProperty("image")]
            public string ImageUri { get; set; } = string.Empty;

            [JsonProperty("readme")]
            public string Readme { get; set; } = string.Empty;

            [JsonProperty("download")]
            public string Download { get; set; } = string.Empty;

            [JsonProperty("machineURL")]
            public string MachineURL { get; set; } = string.Empty;
        }

        public static async Task<List<ModlistMetadata>> LoadFromGithub()
        {
            var client = new Common.Http.Client();
            Utils.Log("Loading ModLists from GitHub");
            var metadataResult = client.GetStringAsync(Consts.ModlistMetadataURL);
            var summaryResult = client.GetStringAsync(Consts.ModlistSummaryURL);

            var metadata = (await metadataResult).FromJsonString<List<ModlistMetadata>>();
            try
            {
                var summaries = (await summaryResult).FromJsonString<List<ModListSummary>>().ToDictionary(d => d.MachineURL);

                foreach (var data in metadata)
                    if (summaries.TryGetValue(data.Links.MachineURL, out var summary))
                        data.ValidationSummary = summary;
            }
            catch (Exception)
            {
                // ignored
            }

            return metadata.OrderBy(m => (m.ValidationSummary?.HasFailures ?? false ? 1 : 0, m.Title)).ToList();
        }
        
        public bool NeedsDownload(AbsolutePath modlistPath)
        {
            if (!modlistPath.Exists) return true;
            if (DownloadMetadata?.Hash == null)
            {
                return true;
            }
            return DownloadMetadata.Hash != modlistPath.FileHashCached(true);
        }
    }

    [JsonName("DownloadMetadata")]
    public class DownloadMetadata
    {
        public Hash Hash { get; set; }
        public long Size { get; set; }

        public long NumberOfArchives { get; set; }
        public long SizeOfArchives { get; set; }
        public long NumberOfInstalledFiles { get; set; }
        public long SizeOfInstalledFiles { get; set; }
    }

    [JsonName("ModListSummary")]
    public class ModListSummary
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("machineURL")]
        public string MachineURL { get; set; } = string.Empty;

        [JsonProperty("checked")]
        public DateTime Checked { get; set; }
        [JsonProperty("failed")]
        public int Failed { get; set; }
        [JsonProperty("passed")]
        public int Passed { get; set; }
        [JsonProperty("updating")]
        public int Updating { get; set; }

        [JsonProperty("link")]
        public string Link => $"/lists/status/{MachineURL}.json";
        [JsonProperty("report")]
        public string Report => $"/lists/status/{MachineURL}.html";
        [JsonProperty("has_failures")]
        public bool HasFailures => Failed > 0;
    }

}
