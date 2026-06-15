using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Validation;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wabbajack.DTOs.Test;

[ClassConstructor<DtosClassConstructor>]
public class ModListTests
{
    private readonly HttpClient _client;
    private readonly ILogger<ModListTests> _logger;
    private readonly ParallelOptions _parallelOptions;
    private readonly DTOSerializer _serializer;
    private readonly Client _wjClient;

    public ModListTests(ILogger<ModListTests> logger, DTOSerializer serializer, HttpClient client, Client wjClient,
        ParallelOptions parallelOptions)
    {
        _serializer = serializer;
        _client = client;
        _wjClient = wjClient;
        _logger = logger;
        _parallelOptions = parallelOptions;
    }

    [Test]
    public async Task CanLoadModListSummaryInfo()
    {
        var jsonPath = KnownFolders.EntryPoint.Combine(@"Resources\ModListSummarySample.json");
        var data = JsonSerializer.Deserialize<ModListSummary[]>(jsonPath.ReadAllText());
        await Assert.That(data).IsNotNull();
        await Assert.That(data.Length).IsEqualTo(38);
    }

    [Test]
    public async Task CanLoadModList()
    {
        var jsonPath = KnownFolders.EntryPoint.Combine(@"Resources\ModListSample.json");
        var data = _serializer.Deserialize<ModList>(jsonPath.ReadAllText());
        var s = _serializer.Serialize(data);
    }

    [Test]
    [Arguments("https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/modlists.json")]
    public async Task CanLoadModListMetadata(string uri)
    {
        var str = await _client.GetStringAsync(uri);
        var data = _serializer.Deserialize<ModlistMetadata[]>(str);
        var s = _serializer.Serialize(data);
        await Assert.That(data!.Length > 0).IsTrue();
    }

    [Test]
    [Arguments("https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml")]
    public async Task CanLoadAllowList(string uri)
    {
        var str = await _client.GetStringAsync(uri);
        var d = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        var list = d.Deserialize<ServerAllowList>(str);

        await Assert.That(list.GoogleIDs.Length > 1).IsTrue();
        await Assert.That(list.AllowedPrefixes.Length > 1).IsTrue();
    }

    [Test]
    public async Task CanGetListStatus()
    {
        var statuses = await _wjClient.GetListStatuses();
        await Assert.That(statuses.Length > 10).IsTrue();

        await statuses.PDoAll(new Resource<ModListTests>("Resource Test", 4),
            async status =>
            {
                _logger.LogInformation("Loading {machineURL}", status.MachineURL);
                var detailed = await _wjClient.GetDetailedStatus(status.MachineURL.Split('/')[0], status.MachineURL.Split('/')[1]);
                await Assert.That(detailed.MachineURL == status.MachineURL).IsTrue();
            });
    }
}