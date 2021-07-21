using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using Wabbajack.Common;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Lib.GitHub
{
    public class Client
    {
        private readonly GitHubClient _client;

        private Client(GitHubClient client)
        {
            _client = client;
        }
        public static async Task<Client> Get()
        {
            if (!Utils.HaveEncryptedJson("github-key"))
                return new Client(new GitHubClient(ProductHeaderValue.Parse("wabbajack")));

            var key = Encoding.UTF8.GetString(await Utils.FromEncryptedData("github-key"));
            var creds = new Credentials(key);
            return new Client(new GitHubClient(ProductHeaderValue.Parse("wabbajack_build_server")){Credentials = creds});
        }

        public enum List
        {
            CI,
            Unlisted,
            Utility,
            Published
        }

        public Dictionary<List, string> PathNames = new()
        {
            {List.CI, "ci_lists.json"},
            {List.Unlisted, "unlisted_modlists.json"},
            {List.Utility, "utility_modlists.json"},
            {List.Published, "modlists.json"}
        };

        public async Task<(string Hash, IReadOnlyList<ModlistMetadata> Lists)> GetData(List lst)
        {
            var content =
                (await _client.Repository.Content.GetAllContents("wabbajack-tools", "mod-lists", PathNames[lst])).First();
            return (content.Sha, content.Content.FromJsonString<ModlistMetadata[]>());
        }
        
        private async Task WriteData(List file, string machinenUrl, string dataHash, IReadOnlyList<ModlistMetadata> dataLists)
        {
            var listData = dataLists.ToJson(prettyPrint: true);
            // the website requires all names be in lowercase;
            listData = GameRegistry.Games.Keys.Aggregate(listData, 
                (current, g) => current.Replace($"\"game\": \"{g}\",", $"\"game\": \"{g.ToString().ToLower()}\","));

            var updateRequest = new UpdateFileRequest($"New release of {machinenUrl}", listData, dataHash);
            await _client.Repository.Content.UpdateFile("wabbajack-tools", "mod-lists", PathNames[file], updateRequest);
        }

        public async Task UpdateList(string maintainer, UpdateRequest newData)
        {
            foreach (var file in Enum.GetValues<List>())
            {
                var data = await GetData(file);
                var found = data.Lists.FirstOrDefault(l => l.Links.MachineURL == newData.MachineUrl && l.Maintainers.Contains(maintainer));
                if (found == null) continue;

                found.DownloadMetadata = newData.DownloadMetadata;
                found.Version = newData.Version;
                found.Links.Download = newData.DownloadUrl.ToString();

                await WriteData(file, newData.MachineUrl, data.Hash, data.Lists);
                return;
            }

            throw new Exception("List not found or user not authorized");
        }


    }
}
