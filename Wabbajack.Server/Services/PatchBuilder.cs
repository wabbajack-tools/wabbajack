using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Splat;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Wabbajack.Server.Services
{
    public class PatchBuilder : AbstractService<PatchBuilder, int>
    {
        private DiscordWebHook _discordWebHook;
        private SqlService _sql;
        private ArchiveMaintainer _maintainer;

        public PatchBuilder(ILogger<PatchBuilder> logger, SqlService sql, AppSettings settings, ArchiveMaintainer maintainer,
            DiscordWebHook discordWebHook, QuickSync quickSync) : base(logger, settings, quickSync, TimeSpan.FromMinutes(1))
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
                count++;

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

                    if (patch.Src.Archive.Hash == patch.Dest.Archive.Hash)
                    {
                        await patch.Fail(_sql, "Hashes match");
                        continue;
                    }

                    if (patch.Src.Archive.Size > 2_500_000_000 || patch.Dest.Archive.Size > 2_500_000_000)
                    {
                        await patch.Fail(_sql, "Too large to patch");
                        continue;
                    }

                    _maintainer.TryGetPath(patch.Src.Archive.Hash, out var srcPath);
                    _maintainer.TryGetPath(patch.Dest.Archive.Hash, out var destPath);

                    var patchName = $"{Consts.ArchiveUpdatesCDNFolder}\\{patch.Src.Archive.Hash.ToHex()}_{patch.Dest.Archive.Hash.ToHex()}";

                    await using var sigFile = new TempFile();
                    await using var patchFile = new TempFile();
                    await using var srcStream = await srcPath.OpenShared();
                    await using var destStream = await destPath.OpenShared();
                    await using var sigStream = await sigFile.Path.Create();
                    await using var patchOutput = await patchFile.Path.Create();
                    OctoDiff.Create(destStream, srcStream, sigStream, patchOutput);
                    await patchOutput.DisposeAsync();
                    var size = patchFile.Path.Size;

                    await UploadToCDN(patchFile.Path, patchName);
                   
                    
                    await patch.Finish(_sql, size);
                    await _discordWebHook.Send(Channel.Spam,
                        new DiscordMessage
                        {
                            Content =
                                $"Built {size.ToFileSizeString()} patch from {patch.Src.Archive.State.PrimaryKeyString} to {patch.Dest.Archive.State.PrimaryKeyString}"
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while building patch");
                    await patch.Fail(_sql, ex.ToString());
                    await _discordWebHook.Send(Channel.Spam,
                        new DiscordMessage
                        {
                            Content =
                                $"Failure building patch from {patch.Src.Archive.State.PrimaryKeyString} to {patch.Dest.Archive.State.PrimaryKeyString}"
                        });                    

                }
            }

            if (count > 0)
            {
                // Notify the List Validator that we may have more patches
                await _quickSync.Notify<ListValidator>();
            }

            return count;
        }

        private async Task UploadToCDN(AbsolutePath patchFile, string patchName)
        {
            for (var times = 0; times < 5; times ++)
            {
                try
                {
                    _logger.Log(LogLevel.Information,
                        $"Uploading {patchFile.Size.ToFileSizeString()} patch file to CDN");
                    using var client = await GetBunnyCdnFtpClient();
                    
                    if (!await client.DirectoryExistsAsync(Consts.ArchiveUpdatesCDNFolder)) 
                        await client.CreateDirectoryAsync(Consts.ArchiveUpdatesCDNFolder);
                    
                    await client.UploadFileAsync((string)patchFile, patchName, FtpRemoteExists.Overwrite);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error uploading {patchFile} to CDN");
                }
            }
            _logger.Log(LogLevel.Error, $"Couldn't upload {patchFile} to {patchName}");
        }

        private async Task<FtpClient> GetBunnyCdnFtpClient()
        {
            var info = await Utils.FromEncryptedJson<BunnyCdnFtpInfo>("bunny-cdn-ftp-info");
            var client = new FtpClient(info.Hostname) {Credentials = new NetworkCredential(info.Username, info.Password)};
            await client.ConnectAsync();
            return client;
        }

    }
}
