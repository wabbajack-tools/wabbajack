using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wabbajack.Networking.WabbajackClientApi
{
    public class Client
    {
        public static readonly long UploadedFileBlockSize = (long)1024 * 1024 * 2;
        
        private readonly HttpClient _client;

        private readonly Configuration _configuration;
        private readonly ILogger<Client> _logger;
        private readonly DTOSerializer _dtos;
        private readonly ParallelOptions _parallelOptions;


        public Client(ILogger<Client> logger, HttpClient client, Configuration configuration, DTOSerializer dtos, ParallelOptions parallelOptions)
        {
            _configuration = configuration;
            _client = client;
            _logger = logger;
            _logger.LogInformation("File hash check (-42) {key}", _configuration.MetricsKey);
            _dtos = dtos;
            _parallelOptions = parallelOptions;
        }

        public async Task SendMetric(string action, string subject)
        {
            var msg = new HttpRequestMessage(HttpMethod.Get,
                $"{_configuration.BuildServerUrl}metrics/{action}/{subject}");
            msg.Headers.Add(_configuration.MetricsKeyHeader, _configuration.MetricsKey);
            await _client.SendAsync(msg);
        }

        public async Task<ServerAllowList> LoadDownloadAllowList()
        {
            var str = await _client.GetStringAsync(_configuration.ServerAllowList);
            var d = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();
            return d.Deserialize<ServerAllowList>(str);
        }
        
        public async Task<ServerAllowList> LoadMirrorAllowList()
        {
            var str = await _client.GetStringAsync(_configuration.MirrorAllowList);
            var d = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();
            return d.Deserialize<ServerAllowList>(str);
        }
        
        public async Task<Dictionary<Hash, ValidatedArchive>> LoadUpgradedArchives()
        {
            return (await _client.GetFromJsonAsync<ValidatedArchive[]>(_configuration.UpgradedArchives, _dtos.Options))!.ToDictionary(d => d.Original.Hash);
        }
        
        public async Task<Archive[]> GetGameArchives(Game game, string version)
        {
            var url = $"https://raw.githubusercontent.com/wabbajack-tools/indexed-game-files/master/{game}/{version}.json";
            return await _client.GetFromJsonAsync<Archive[]>(url) ?? Array.Empty<Archive>();
        }
        
        public async Task<Archive[]> GetArchivesForHash(Hash hash)
        {
            return await _client.GetFromJsonAsync<Archive[]>(
                $"{_configuration.BuildServerUrl}mod_files/by_hash/{hash.ToHex()}", _dtos.Options) ?? Array.Empty<Archive>();
        }

        public async Task<Uri?> GetMirrorUrl(Hash archiveHash)
        {
            try
            {
                var result =
                    await _client.GetStringAsync($"{_configuration.BuildServerUrl}mirror/{archiveHash.ToHex()}");
                return new Uri(result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "While downloading mirror for {hash}", archiveHash);
                return null;
            }
        }

        public async Task SendModListDefinition(ModList modList)
        {

            await using var fs = new MemoryStream();
            await using var gzip = new GZipStream(fs, CompressionLevel.SmallestSize, true);
            await _dtos.Serialize(modList, gzip);
            await gzip.DisposeAsync();
            fs.Position = 0;

            var msg = new HttpRequestMessage(HttpMethod.Post,
                $"{_configuration.BuildServerUrl}list_definitions/ingest");
            msg.Headers.Add("x-compressed-body", "gzip");
            msg.Content = new StreamContent(fs);
            await _client.SendAsync(msg);
        }

        public async Task<ModListSummary[]> GetListStatuses()
        {
            return await _client.GetFromJsonAsync<ModListSummary[]>(
                $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/modListSummary.json", _dtos.Options) ?? Array.Empty<ModListSummary>();

        }

        public async Task<ValidatedModList> GetDetailedStatus(string machineURL)
        {
            return (await _client.GetFromJsonAsync<ValidatedModList>(
                $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{machineURL}/status.json", _dtos.Options))!;
        }

        public async Task<FileDefinition> GenerateFileDefinition(AbsolutePath path)
        {
            IEnumerable<PartDefinition> Blocks(AbsolutePath path)
            {
                var size = path.Size();
                for (long block = 0; block * UploadedFileBlockSize < size; block ++)
                    yield return new PartDefinition
                    {
                        Index = block,
                        Size = Math.Min(UploadedFileBlockSize, size - block * UploadedFileBlockSize),
                        Offset = block * UploadedFileBlockSize
                    };
            }
            
            var parts = Blocks(path).ToArray();
            var definition = new FileDefinition
            {
                OriginalFileName = path.FileName, 
                Size = path.Size(), 
                Hash = await path.Hash(),
                Parts = await parts.PMap(_parallelOptions, async part =>
                {
                    var buffer = new byte[part.Size];
                    await using (var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.Position = part.Offset;
                        await fs.ReadAsync(buffer);
                    }
                    part.Hash = await buffer.Hash();
                    return part;
                }).ToArray()
            };

            return definition;

        }

        public async Task<ModlistMetadata[]> LoadLists(bool includeUnlisted = false)
        {
            var lists = (new[] {"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/modlists.json",
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/utility_modlists.json",
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/unlisted_modlists.json"})
                .Take(includeUnlisted ? 3 : 2);

            return await lists.PMap(_parallelOptions, async url => await _client.GetFromJsonAsync<ModlistMetadata[]>(url, _dtos.Options)!)
                .SelectMany(x => x)
                .ToArray();

        }

        public Uri GetPatchUrl(Hash upgradeHash, Hash archiveHash)
        {
            return new Uri($"{_configuration.PatchBaseAddress}{upgradeHash.ToHex()}_{archiveHash.ToHex()}");
        }
    }
}