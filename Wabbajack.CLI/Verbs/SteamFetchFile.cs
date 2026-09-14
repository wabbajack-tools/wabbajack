using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Fetches one named file out of a Steam depot.
///     The file it writes is verified against the hash the depot manifest carries for it before it is
///     accepted, and the verb then prints Wabbajack's own xxHash64 of the result, which is the hash a
///     modlist records a game file by.
/// </summary>
public class SteamFetchFile
{
    private readonly SteamContentClient _content;
    private readonly ILogger<SteamFetchFile> _logger;
    private readonly ISteamSession _session;

    public SteamFetchFile(ILogger<SteamFetchFile> logger, ISteamSession session, SteamContentClient content)
    {
        _logger = logger;
        _session = session;
        _content = content;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-fetch-file",
        "Fetches a single file out of a Steam depot manifest", new[]
        {
            new OptionDefinition(typeof(uint), "a", "app", "Steam app id"),
            new OptionDefinition(typeof(uint), "d", "depot", "Steam depot id"),
            new OptionDefinition(typeof(ulong), "m", "manifest",
                "Steam manifest id. Omit for whatever the depot is publishing on the public branch now"),
            new OptionDefinition(typeof(string), "f", "file",
                "Path of the wanted file inside the depot, e.g. Data\\Skyrim.esm"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Where to write the file")
        });

    public async Task<int> Run(uint app, uint depot, ulong manifest, string file, AbsolutePath output,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            _logger.LogError("--file is required: say which file in the depot to fetch");
            return 1;
        }

        if (output == default)
        {
            _logger.LogError("--output is required: say where to write the file");
            return 1;
        }

        try
        {
            await SteamVerbSupport.EnsureLoggedInAsync(_session, token);

            // Entitlement before anything else, so an account that cannot reach the depot is told that
            // rather than being walked through a manifest lookup that was never going to work.
            await _content.EnsureAccessAsync(app, depot, token);

            var manifestId = await SteamVerbSupport.ResolveManifestAsync(_content, _logger, app, depot, manifest);

            var fetched = await _content.DownloadFileAsync(app, depot, manifestId, file, output, token);

            await using var stream = output.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await stream.HashingCopy(Stream.Null, token);

            _logger.LogInformation("Depot path   : {Path}", fetched.Path);
            _logger.LogInformation("Size         : {Size} bytes", fetched.Size);
            _logger.LogInformation("Manifest SHA1: {Sha1}", fetched.Sha1);
            _logger.LogInformation("xxHash64     : {Hash} ({Hex})", hash, hash.ToHex());
            _logger.LogInformation("Written to   : {Output}", output);

            return 0;
        }
        catch (Exception ex) when (SteamVerbSupport.Report(_logger, ex) is { } code)
        {
            return code;
        }
    }
}
