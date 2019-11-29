using System.Collections.Generic;
using System.Net.Http;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Wabbajack.Common;
using File = System.IO.File;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Lib.ModListRegistry
{
    public class ModlistMetadata
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("game")]
        public Game Game { get; set; }

        [JsonIgnore] public string GameName => Game.ToDescriptionString();

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("links")]
        public LinksObject Links { get; set; } = new LinksObject();

        [JsonProperty("download_metadata")]
        public DownloadMetadata DownloadMetadata { get; set; }

        public class LinksObject
        {
            [JsonProperty("image")]
            public string ImageUri { get; set; }

            [JsonIgnore]
            public BitmapImage Image { get; set; }

            [JsonProperty("readme")]
            public string Readme { get; set; }

            [JsonProperty("download")]
            public string Download { get; set; }
            
            [JsonProperty("machineURL")]
            public string MachineURL { get; set; }
        }





        public static List<ModlistMetadata> LoadFromGithub()
        {
            var client = new HttpClient();
            Utils.Log("Loading ModLists from Github");
            var result = client.GetStringSync(Consts.ModlistMetadataURL); 
            return result.FromJSONString<List<ModlistMetadata>>();
        }

        public bool NeedsDownload(string modlistPath)
        {
            if (!File.Exists(modlistPath)) return true;
            if (DownloadMetadata?.Hash == null)
            {
                return true;
            }
            return DownloadMetadata.Hash != modlistPath.FileHashCached();
        }
    }

    public class DownloadMetadata
    {
        public string Hash { get; set; }
        public long Size { get; set; }

        public long NumberOfArchives { get; set; }
        public long SizeOfArchives { get; set; }
        public long NumberOfInstalledFiles { get; set; }
        public long SizeOfInstalledFiles { get; set; }

    }

}
