using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using Wabbajack.DTOs;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.GitHub.DTOs;

namespace Wabbajack.Networking.GitHub;

public class Client
{
    private readonly GitHubClient _client;
    private readonly DTOSerializer _dtos;
    private readonly ILogger<Client> _logger;
    private readonly HttpClient _httpClient;

    public Client(ILogger<Client> logger, DTOSerializer dtos, GitHubClient client, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _logger = logger;
        _client = client;
        _dtos = dtos;
    }

    public async Task<(string Hash, IReadOnlyList<ModlistMetadata> Lists)> GetData(List lst)
    {
        var content =
            (await _client.Repository.Content.GetAllContents("wabbajack-tools", "mod-lists", PathNames.FromList[lst]))
            .First();
        return (content.Sha, _dtos.Deserialize<ModlistMetadata[]>(content.Content)!);
    }

    private async Task WriteData(List file, string machinenUrl, string dataHash,
        IReadOnlyList<ModlistMetadata> dataLists)
    {
        var listData = _dtos.Serialize(dataLists, true);
        // the website requires all names be in lowercase;
        listData = GameRegistry.Games.Keys.Aggregate(listData,
            (current, g) => current.Replace($"\"game\": \"{g}\",", $"\"game\": \"{g.ToString().ToLower()}\","));

        var updateRequest = new UpdateFileRequest($"New release of {machinenUrl}", listData, dataHash);
        await _client.Repository.Content.UpdateFile("wabbajack-tools", "mod-lists", PathNames.FromList[file],
            updateRequest);
    }

    public async Task UpdateList(string maintainer, UpdateRequest newData)
    {
        foreach (var file in Enum.GetValues<List>())
        {
            var data = await GetData(file);
            var found = data.Lists.FirstOrDefault(l =>
                l.NamespacedName == newData.MachineUrl && l.Maintainers.Contains(maintainer));
            if (found == null) continue;

            found.DownloadMetadata = newData.DownloadMetadata;
            found.Version = newData.Version;
            found.Links.Download = newData.DownloadUrl.ToString();

            await WriteData(file, newData.MachineUrl, data.Hash, data.Lists);
            return;
        }

        throw new Exception("List not found or user not authorized");
    }

    public async Task<(string Sha, string Content)> GetData(string owner, string repo, string path)
    {
        var result = (await _client.Repository.Content.GetAllContents(owner, repo, path))[0];
        return (result.Sha, result.Content);
    }

    public async Task<UserInfo?> GetUserInfoFromPAT(string pat)
    {
        var msg = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        msg.Headers.Add("User-Agent", "wabbajack");
        msg.Headers.Add("Authorization", "Token " + pat);
        var result = await _httpClient.SendAsync(msg);
        if (!result.IsSuccessStatusCode) return null;
        return await result.Content.ReadFromJsonAsync<UserInfo>();
    }

    public async Task PutData(string owner, string repo, string path, string message, string content, string oldSha)
    {
        await _client.Repository.Content.UpdateFile(owner, repo, path, new UpdateFileRequest(message, content, oldSha));
    }

    public async Task<IReadOnlyList<RepositoryContributor>> GetWabbajackContributors()
    {
        return await _client.Repository.GetAllContributors("wabbajack-tools", "wabbajack");
    }
}