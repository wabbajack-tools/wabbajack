using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Utilities.Collections;
using Wabbajack.BuildServer;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class MirrorUploader : AbstractService<MirrorUploader, int>
    {
        private SqlService _sql;
        private ArchiveMaintainer _archives;

        public MirrorUploader(ILogger<MirrorUploader> logger, AppSettings settings, SqlService sql, QuickSync quickSync, ArchiveMaintainer archives) : base(logger, settings, quickSync, TimeSpan.FromHours(1))
        {
            _sql = sql;
            _archives = archives;
        }

        public override async Task<int> Execute()
        {
        
            int uploaded = 0;
            TOP:
            var toUpload = await _sql.GetNextMirroredFile();
            if (toUpload == default) return uploaded;
            uploaded += 1;

            try
            {
                var creds = await BunnyCdnFtpInfo.GetCreds(StorageSpace.Mirrors);

                using var queue = new WorkQueue();
                if (_archives.TryGetPath(toUpload.Hash, out var path))
                {
                    _logger.LogInformation($"Uploading mirror file {toUpload.Hash} {path.Size.FileSizeToString()}");

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

                    var definition = await Client.GenerateFileDefinition(queue, path, (s, percent) => { });

                    using (var client = await GetClient(creds))
                    {
                        await client.CreateDirectoryAsync($"{definition.Hash.ToHex()}");
                        await client.CreateDirectoryAsync($"{definition.Hash.ToHex()}/parts");
                    }

                    string MakePath(long idx)
                    {
                        return $"{definition.Hash.ToHex()}/parts/{idx}";
                    }

                    await definition.Parts.PMap(queue, async part =>
                    {
                        _logger.LogInformation($"Uploading mirror part ({part.Index}/{definition.Parts.Length})");
                        var name = MakePath(part.Index);
                        var buffer = new byte[part.Size];
                        await using (var fs = await path.OpenShared())
                        {
                            fs.Position = part.Offset;
                            await fs.ReadAsync(buffer);
                        }

                        using var client = await GetClient(creds);
                        await client.UploadAsync(new MemoryStream(buffer), name);
                    });

                    using (var client = await GetClient(creds))
                    {
                        _logger.LogInformation($"Finishing mirror upload");
                        await using var ms = new MemoryStream();
                        await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                        {
                            definition.ToJson(gz);
                        }

                        ms.Position = 0;
                        await client.UploadAsync(ms, $"{definition.Hash.ToHex()}/definition.json.gz");
                    }

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

        private static async Task<FtpClient> GetClient(BunnyCdnFtpInfo creds)
        {
            var ftpClient = new FtpClient(creds.Hostname, new NetworkCredential(creds.Username, creds.Password));
            await ftpClient.ConnectAsync();
            return ftpClient;
        }
    }
}
