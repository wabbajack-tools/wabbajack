using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Splat;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class PatchBuilder : AbstractService<PatchBuilder, int>
    {
        private DiscordWebHook _discordWebHook;
        private SqlService _sql;
        private ArchiveMaintainer _maintainer;

        public PatchBuilder(ILogger<PatchBuilder> logger, SqlService sql, AppSettings settings, ArchiveMaintainer maintainer,
            DiscordWebHook discordWebHook) : base(logger, settings, TimeSpan.FromMinutes(1))
        {
            _discordWebHook = discordWebHook;
            _sql = sql;
            _maintainer = maintainer;
        }

        public override async Task<int> Execute()
        {
            int count = 0;
            while (true)
            {
                var patch = await _sql.GetPendingPatch();
                if (patch == default) break;

                try
                {

                    _logger.LogInformation(
                        $"Building patch from {patch.Src.Archive.State.PrimaryKeyString} to {patch.Dest.Archive.State.PrimaryKeyString}");
                    await _discordWebHook.Send(Channel.Spam,
                        new DiscordMessage
                        {
                            Content =
                                $"Building patch from {patch.Src.Archive.State.PrimaryKeyString} to {patch.Dest.Archive.State.PrimaryKeyString}"
                        });

                    _maintainer.TryGetPath(patch.Src.Archive.Hash, out var srcPath);
                    _maintainer.TryGetPath(patch.Dest.Archive.Hash, out var destPath);

                    var patchName = $"archive_updates\\{patch.Src.Archive.Hash}_{patch.Dest.Archive.Hash}";

                    using var sigFile = new TempFile();
                    await using var srcStream = srcPath.OpenShared();
                    await using var destStream = destPath.OpenShared();
                    await using var sigStream = sigFile.Path.Create();
                    using var ftpClient = await GetBunnyCdnFtpClient();

                    if (!await ftpClient.DirectoryExistsAsync("archive_updates")) 
                        await ftpClient.CreateDirectoryAsync("archive_updates");

                    
                    await using var patchOutput = await ftpClient.OpenWriteAsync(patchName);
                    OctoDiff.Create(destStream, srcStream, sigStream, patchOutput);
                    
                    await patchOutput.DisposeAsync();
                    
                    var size = await ftpClient.GetFileSizeAsync(patchName);
                    
                    await patch.Finish(_sql, size);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while building patch");
                    await patch.Fail(_sql, ex.ToString());
                }

                count++;
            }

            return count;
        }
        
        private async Task<FtpClient> GetBunnyCdnFtpClient()
        {
            var info = Utils.FromEncryptedJson<BunnyCdnFtpInfo>("bunny-cdn-ftp-info");
            var client = new FtpClient(info.Hostname) {Credentials = new NetworkCredential(info.Username, info.Password)};
            await client.ConnectAsync();
            return client;
        }

    }
}
