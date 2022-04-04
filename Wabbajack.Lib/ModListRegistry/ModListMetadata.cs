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

        [JsonProperty("maintainers")] public string[] 
            Maintainers { get; set; } = Array.Empty<string>();

        [JsonProperty("game")]
        public Game Game { get; set; }

        [JsonIgnore] public string GameName => Game.ToDescriptionString();

        [JsonProperty("official")]
        public bool Official { get; set; }

        [JsonProperty("tags")]
        public List<string> tags { get; set; } = new List<string>();

        [JsonProperty("nsfw")]
        public bool NSFW { get; set; }

        [JsonProperty("utility_list")]
        public bool UtilityList { get; set; }

        [JsonProperty("image_contains_title")]
        public bool ImageContainsTitle { get; set; }

        [JsonProperty("force_down")]
        public bool ForceDown { get; set; }

        [JsonProperty("links")]
        public LinksObject Links { get; set; } = new LinksObject();

        [JsonProperty("download_metadata")]
        public DownloadMetadata? DownloadMetadata { get; set; }
        
        [JsonProperty("version")]
        public Version? Version { get; set; }

        [JsonIgnore]
        public ModListSummary ValidationSummary { get; set; } = new ModListSummary();
        [JsonName("repositoryName")] public string RepositoryName { get; set; } = string.Empty;
        [JsonIgnore] public string NamespacedName => $"{RepositoryName}/{Links.MachineURL}";
        
        [JsonIgnore] public bool IsFeatured { get; set; }

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
            internal string MachineURL { get; set; } = string.Empty;

            [JsonProperty("discordURL")] 
            public string DiscordURL { get; set; } = string.Empty;
        }

        public static async Task<List<ModlistMetadata>> LoadFromGithub()
        {
            var client = new Http.Client();
            Utils.Log("Loading ModLists from GitHub");

            var summaryResult = client.GetStringAsync(Consts.ModlistSummaryURL);

            var metadata = await LoadModlists();
            
            
            try
            {
                var summaries = (await summaryResult).FromJsonString<List<ModListSummary>>().ToDictionary(d => d.MachineURL);

                foreach (var data in metadata)
                    if (summaries.TryGetValue(data.NamespacedName, out var summary))
                        data.ValidationSummary = summary;
            }
            catch (Exception)
            {
                // ignored
            }

            var random = new Random();
            return metadata
                // Sort randomly initially, just to give each list a fair shake
                .Shuffle(random)
                // Put broken lists at bottom
                .OrderBy(m => (m.ValidationSummary?.HasFailures ?? false ? 1 : 0))
                .ToList();
        }
        
        public static async Task<Dictionary<string, Uri>> LoadRepositories()
        {
            var client = new Http.Client();
            var repositories = (await client.GetStringAsync("https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/repositories.json"))
                .FromJsonString<Dictionary<string, Uri>>();
            return repositories!;
        }

        public static async Task<HashSet<string>> LoadFeatured()
        {
            var client = new Http.Client();
            var repositories = (await client.GetStringAsync("https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/featured_lists.json"))
                .FromJsonString<string[]>();
            return repositories!.ToHashSet();
        }

        public static async Task<ModlistMetadata[]> LoadModlists()
        {
            var repos = await LoadRepositories();
            var featured = await LoadFeatured();
            List<ModlistMetadata> metadatas = new();
            var client = new Http.Client();
            foreach (var repo in repos)
            {
                try
                {
                    var newData = (await client.GetStringAsync(repo.Value))
                        .FromJsonString<ModlistMetadata[]>()
                        .Select(meta =>
                        {
                            meta.RepositoryName = repo.Key;
                            meta.IsFeatured = meta.RepositoryName == "wj-featured" || featured.Contains(meta.NamespacedName);
                            return meta;
                        });
                    metadatas.AddRange(newData);
                }
                catch (JsonException je)
                {
                    Utils.Log($"Parsing {repo.Key} got a json parse exception {je}");
                }
            }

            return metadatas.ToArray();
        }

        public async ValueTask<bool> NeedsDownload(AbsolutePath modlistPath)
        {
            if (!modlistPath.Exists) return true;
            if (DownloadMetadata?.Hash == null)
            {
                return true;
            }
            return DownloadMetadata.Hash != await modlistPath.FileHashCachedAsync();
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

        [JsonProperty("mirrored")]
        public int Mirrored { get; set; }

        [JsonProperty("link")]
        public string Link => $"/lists/status/{MachineURL}.json";
        [JsonProperty("report")]
        public string Report => $"/lists/status/{MachineURL}.html";
        
        [JsonProperty("modlist_missing")]
        public bool ModListIsMissing { get; set; }
        
        [JsonProperty("has_failures")]
        public bool HasFailures => Failed > 0 || ModListIsMissing;
    }

}
