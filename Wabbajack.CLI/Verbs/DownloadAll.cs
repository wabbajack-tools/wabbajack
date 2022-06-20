using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class DownloadAll : IVerb
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly ILogger<DownloadAll> _logger;
    private readonly Client _wjClient;
    private readonly DTOSerializer _dtos;
    private readonly Resource<DownloadAll> _limiter;
    private readonly FileHashCache _cache;

    public const int MaxDownload = 6000;

    public DownloadAll(ILogger<DownloadAll> logger, DownloadDispatcher dispatcher, Client wjClient, DTOSerializer dtos, FileHashCache cache)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _wjClient = wjClient;
        _dtos = dtos;
        _limiter = new Resource<DownloadAll>("Download All", 16);
        _cache = cache;
    }

    public Command MakeCommand()
    {
        var command = new Command("download-all");
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Output folder"));
        command.Description = "Downloads all files for all modlists in the gallery";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task<int> Run(AbsolutePath output, CancellationToken token)
    {
        _logger.LogInformation("Downloading modlists");

        var existing = await output.EnumerateFiles()
            .Where(f => f.Extension != Ext.Meta)
            .PMapAll(_limiter, async f =>
            {
                _logger.LogInformation("Hashing {File}", f.FileName);
                return await _cache.FileHashCachedAsync(f, token);
            })
            .ToHashSet();

        var archives = (await (await _wjClient.LoadLists())
                .PMapAll(_limiter, async m =>
                {
                    try
                    {
                        return await StandardInstaller.Load(_dtos, _dispatcher, m, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "While downloading list");
                        return default;
                    }
                })
                .Where(d => d != default)
                .SelectMany(m => m!.Archives)
                .ToList())
            .DistinctBy(d => d.Hash)
            .Where(d => d.State is Nexus)
            .Where(d => !existing.Contains(d.Hash))
            .ToList();


        
        _logger.LogInformation("Found {Count} Archives totaling {Size}", archives.Count, archives.Sum(a => a.Size).ToFileSizeString());

        await archives
            .OrderBy(a => a.Size)
            .Take(MaxDownload)
            .PDoAll(_limiter, async file => {
                var outputFile = output.Combine(file.Name);
            if (outputFile.FileExists())
            {
                outputFile = output.Combine(outputFile.FileName.WithoutExtension() + "_" + file.Hash.ToHex() +
                 outputFile.WithExtension(outputFile.Extension));
            }
            
            _logger.LogInformation("Downloading {File}", file.Name);

            try
            {
                var result = await _dispatcher.DownloadWithPossibleUpgrade(file, outputFile, token);
                if (result.Item1 == DownloadResult.Failure)
                {
                    if (outputFile.FileExists())
                        outputFile.Delete();
                    return;
                }

                _cache.FileHashWriteCache(output, result.Item2);

                var metaFile = outputFile.WithExtension(Ext.Meta);
                await metaFile.WriteAllTextAsync(_dispatcher.MetaIniSection(file), token: token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While downloading {Name}, Ignoring", file.Name);
            }

            });
            

        return 0;
    } 
}