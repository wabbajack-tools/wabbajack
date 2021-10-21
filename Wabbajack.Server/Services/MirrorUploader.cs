using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using FluentFTP.Helpers;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.TokenProviders;

namespace Wabbajack.Server.Services
{
    public class MirrorUploader : AbstractService<MirrorUploader, int>
    {
        private SqlService _sql;
        private ArchiveMaintainer _archives;
        private DiscordWebHook _discord;
        private readonly IFtpSiteCredentials _credentials;
        private readonly Client _wjClient;
        private readonly ParallelOptions _parallelOptions;
        private readonly DTOSerializer _dtos;
        private readonly IFtpSiteCredentials _ftpCreds;

        public bool ActiveFileSyncEnabled { get; set; } = true;

        public MirrorUploader(ILogger<MirrorUploader> logger, AppSettings settings, SqlService sql, QuickSync quickSync, ArchiveMaintainer archives,
            DiscordWebHook discord, IFtpSiteCredentials credentials, Client wjClient, ParallelOptions parallelOptions, DTOSerializer dtos,
            IFtpSiteCredentials ftpCreds)
            : base(logger, settings, quickSync, TimeSpan.FromHours(1))
        {
            _sql = sql;
            _archives = archives;
            _discord = discord;
            _credentials = credentials;
            _wjClient = wjClient;
            _parallelOptions = parallelOptions;
            _dtos = dtos;
            _ftpCreds = ftpCreds;
        }

        public override async Task<int> Execute()
        {
        
            int uploaded = 0;
            
            if (ActiveFileSyncEnabled)
                await _sql.SyncActiveMirroredFiles();
            TOP:
            var toUpload = await _sql.GetNextMirroredFile();
            if (toUpload == default)
            {
                await DeleteOldMirrorFiles();
                return uploaded;
            }
            uploaded += 1;

            try
            {
                var creds = (await _credentials.Get())[StorageSpace.Mirrors];
                
                if (_archives.TryGetPath(toUpload.Hash, out var path))
                {
                    _logger.LogInformation($"Uploading mirror file {toUpload.Hash} {path.Size().FileSizeToString()}");

                    bool exists = false;
                    using (var client = await GetClient(creds))
                    {
                        exists = await client.FileExistsAsync($"{toUpload.Hash.ToHex()}/definition.json.gz");
                    }
                    
                    if (exists)
                    {
                        _logger.LogInformation($"Skipping {toUpload.Hash} it's already on the server");
                        await toUpload.Finish(_sql);
                        goto TOP;
                    }

                    await _discord.Send(Channel.Spam,
                        new DiscordMessage
                        {
                            Content = $"Uploading {toUpload.Hash} - {toUpload.Created} because {toUpload.Rationale}"
                        });

                    var definition = await _wjClient.GenerateFileDefinition(path);

                    using (var client = await GetClient(creds))
                    {
                        await client.CreateDirectoryAsync($"{definition.Hash.ToHex()}");
                        await client.CreateDirectoryAsync($"{definition.Hash.ToHex()}/parts");
                    }

                    string MakePath(long idx)
                    {
                        return $"{definition.Hash.ToHex()}/parts/{idx}";
                    }

                    await definition.Parts.PDoAll(new Resource<MirrorUploader>(), async part =>
                    {
                        _logger.LogInformation("Uploading mirror part ({index}/{length})", part.Index, definition.Parts.Length);

                        var buffer = new byte[part.Size];
                        await using (var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            fs.Position = part.Offset;
                            await fs.ReadAsync(buffer);
                        }
                        
                        await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>{
                            using var client = await GetClient(creds);
                            var name = MakePath(part.Index);
                            await client.UploadAsync(new MemoryStream(buffer), name);
                        });

                    });

                    await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
                    {
                        using var client = await GetClient(creds);
                        _logger.LogInformation($"Finishing mirror upload");


                        await using var ms = new MemoryStream();
                        await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                        {
                            await _dtos.Serialize(definition, gz);
                        }

                        ms.Position = 0;
                        var remoteName = $"{definition.Hash.ToHex()}/definition.json.gz";
                        await client.UploadAsync(ms, remoteName);
                    });

                    await toUpload.Finish(_sql);
                }
                else
                {
                    await toUpload.Fail(_sql, "Archive not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"{toUpload.Created} {toUpload.Uploaded}");
                _logger.LogError(ex, "Error uploading");
                await toUpload.Fail(_sql, ex.ToString());
            }
            goto TOP;
        }

        private async Task<FtpClient> GetClient(FtpSite? creds = null)
        {
            return await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
            {
                creds ??= (await _ftpCreds.Get())[StorageSpace.Mirrors];

                var ftpClient = new FtpClient(creds.Hostname, new NetworkCredential(creds.Username, creds.Password));
                ftpClient.DataConnectionType = FtpDataConnectionType.EPSV;
                await ftpClient.ConnectAsync();
                return ftpClient;
            });
        }

        /// <summary>
        /// Gets a list of all the Mirrored file hashes that physically exist on the CDN (via FTP lookup)
        /// </summary>
        /// <returns></returns>
        public async Task<HashSet<Hash>> GetHashesOnCDN()
        {
            using var ftpClient = await GetClient();
            var serverFiles = (await ftpClient.GetNameListingAsync("\\"));
            
            return serverFiles
                .Select(f => ((RelativePath)f).FileName)
                .Select(l =>
                {
                    try
                    {
                        return Hash.FromHex((string)l);
                    }
                    catch (Exception) { return default; }
                })
                .Where(h => h != default)
                .ToHashSet();
        }

        public async Task DeleteOldMirrorFiles()
        {
            var existingHashes = await GetHashesOnCDN();
            var fromSql = await _sql.GetAllMirroredHashes();
            
            foreach (var (hash, _) in fromSql.Where(s => s.Value))
            {
                _logger.LogInformation("Removing {hash} from SQL it's no longer in the CDN", hash);
                if (!existingHashes.Contains(hash))
                    await _sql.DeleteMirroredFile(hash);
            }

            var toDelete = existingHashes.Where(h => !fromSql.ContainsKey(h)).ToArray();

            using var client = await GetClient();
            foreach (var hash in toDelete)
            {
                await _discord.Send(Channel.Spam,
                    new DiscordMessage {Content = $"Removing mirrored file {hash}, as it's no longer in sql"});
                _logger.LogInformation("Removing {hash} from the CDN it's no longer in SQL", hash);
                await client.DeleteDirectoryAsync(hash.ToHex());
            }
        }
    }
}
