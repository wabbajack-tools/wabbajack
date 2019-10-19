using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
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

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("links")]
        public LinksObject Links { get; set; } = new LinksObject();

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
            Utils.Log("Loading Modlists from Github");
            var result = client.GetStringSync(Consts.ModlistMetadataURL);
            return result.FromJSONString<List<ModlistMetadata>>();
        }
    }
}
