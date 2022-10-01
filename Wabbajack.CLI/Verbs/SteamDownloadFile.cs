using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.Extensions.Logging;
using SteamKit2;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class SteamDownloadFile : IVerb
{
    private readonly ILogger<SteamDownloadFile> _logger;
    private readonly Client _client;
    private readonly ITokenProvider<SteamLoginState> _token;
    private readonly DepotDownloader _downloader;
    private readonly DTOSerializer _dtos;
    private readonly Wabbajack.Networking.WabbajackClientApi.Client _wjClient;

    public SteamDownloadFile(ILogger<SteamDownloadFile> logger, Client steamClient, ITokenProvider<SteamLoginState> token, 
        DepotDownloader downloader, DTOSerializer dtos, Wabbajack.Networking.WabbajackClientApi.Client wjClient)
    {
        _logger = logger;
        _client = steamClient;
        _token = token;
        _downloader = downloader;
        _dtos = dtos;
        _wjClient = wjClient;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-download-file",
        "Dumps information to the console about the given app",
        new[]
        {
            new OptionDefinition(typeof(string), "g", "game", "Wabbajack game name"),
            new OptionDefinition(typeof(string), "v", "version", "Version of the game to download for"),
            new OptionDefinition(typeof(string), "f", "file", "File to download (relative path)"),
            new OptionDefinition(typeof(string), "o", "output", "Output location")
        });

    internal async Task<int> Run(string gameName, string version, string file, AbsolutePath output)
    {
        if (!GameRegistry.TryGetByFuzzyName(gameName, out var game))
            _logger.LogError("Can't find definition for {Game}", gameName);

        await _client.Login();
        
        var definition = await _wjClient.GetGameArchives(game.Game, version);
        var manifests = await _wjClient.GetSteamManifests(game.Game, version);
        
        _logger.LogInformation("Found {Count} manifests, looking for file", manifests.Length);

        SteamManifest? steamManifest = null;
        DepotManifest? depotManifest = null;
        DepotManifest.FileData? fileData = null;

        var appId = (uint) game.SteamIDs.First();
        
        foreach (var manifest in manifests)
        {
            steamManifest = manifest;
            depotManifest = await _client.GetAppManifest(appId, manifest.Depot, manifest.Manifest);
            fileData = depotManifest.Files!.FirstOrDefault(f => f.FileName == file);
            if (fileData != default)
            {
                break;
            }
        }

        if (fileData == default)
        {
            _logger.LogError("Cannot find {File} in any manifests", file);
            return 1;
        }
        
        _logger.LogInformation("File is {Size} and {ChunkCount} chunks", fileData.TotalSize.FileSizeToString(), fileData.Chunks.Count);

        await _client.Download(appId, depotManifest!.DepotID, steamManifest!.Manifest,  fileData, output, CancellationToken.None);

        _logger.LogInformation("File downloaded");

        return 0;



    }
}