
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using AbsolutePath = Wabbajack.Paths.AbsolutePath;

namespace Wabbajack.CLI.Verbs;

public class IndexNexusMod
{
    private readonly ILogger<IndexNexusMod> _logger;
    private readonly NexusApi _client;
    private readonly TemporaryFileManager _manager;
    private readonly DownloadDispatcher _downloadDispatcher;
    private readonly DTOSerializer _serializer;

    public IndexNexusMod(ILogger<IndexNexusMod> logger, NexusApi nexusClient, TemporaryFileManager manager, DownloadDispatcher downloadDispatcher, DTOSerializer serializer)
    {
        _logger = logger;
        _client = nexusClient;
        _manager = manager;
        _downloadDispatcher = downloadDispatcher;
        _serializer = serializer;
    }

    public static VerbDefinition Definition = new VerbDefinition("index-nexus-mod",
        "Downloads all files for a mod and creates a mirror.json entry for the files", new[]
        {
            new OptionDefinition(typeof(string), "g", "game", "Game Domain"),
            new OptionDefinition(typeof(int), "m", "mod-id", "Nexus mod ID"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output mirror.json file")
        });

    public async Task<int> Run(string game, int modId, AbsolutePath output, CancellationToken token)
    {
        var gameInstance = GameRegistry.GetByFuzzyName(game);
        var modFiles = await _client.ModFiles(game, modId, token);
        _logger.LogInformation("Found {Count} files", modFiles.info.Files.Length);

        var files = new List<Archive>();
        foreach (var file in modFiles.info.Files)
        {
            _logger.LogInformation("Downloading {File}", file.FileName);
            await using var path = _manager.CreateFile();
            var archive = new Archive()
            {
                Name = file.FileName,
                State = new Nexus
                {
                    FileID = file.FileId,
                    Game = gameInstance.Game,
                    ModID = modId,
                    Description = file.Description,
                    Name = file.Name,
                    Version = file.Version
                }
            };

            Hash hash;
            try
            {
                hash = await _downloadDispatcher.Download(archive, path.Path.ToString().ToAbsolutePath(), token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download {File}", file.FileName);
                continue;
            }

            _logger.LogInformation("Downloaded {File} with hash {Hash}", file.FileName, hash);
            archive.Hash = hash;
            archive.Size = file.SizeInBytes!.Value;
            files.Add(archive);
        }

        await using var stream = output.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await _serializer.Serialize(files, stream, true);
        return 0;
    }
}