using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Prints what a depot manifest contains. The point is to be able to answer "what is this file actually
///     called in the depot" without downloading anything, which is most of the work of pointing
///     <c>steam-fetch-file</c> at the right thing.
/// </summary>
public class SteamListManifest
{
    private readonly ISteamContentClient _content;
    private readonly ILogger<SteamListManifest> _logger;
    private readonly ISteamSession _session;

    public SteamListManifest(ILogger<SteamListManifest> logger, ISteamSession session, ISteamContentClient content)
    {
        _logger = logger;
        _session = session;
        _content = content;
    }

    public static VerbDefinition Definition = new VerbDefinition("steam-list-manifest",
        "Lists the files in a Steam depot manifest", new[]
        {
            new OptionDefinition(typeof(uint), "a", "app", "Steam app id"),
            new OptionDefinition(typeof(uint), "d", "depot", "Steam depot id"),
            new OptionDefinition(typeof(ulong), "m", "manifest",
                "Steam manifest id. Omit for whatever the depot is publishing on the public branch now"),
            new OptionDefinition(typeof(string), "f", "filter",
                "Only list entries whose path contains this, case-insensitively"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output",
                "Write the listing to this file instead of the console")
        });

    public async Task<int> Run(uint app, uint depot, ulong manifest, string filter, AbsolutePath output,
        CancellationToken token)
    {
        try
        {
            await SteamVerbSupport.EnsureLoggedInAsync(_session, token);

            // Entitlement first, so an account that does not own this is told so rather than being left to
            // interpret a refused manifest request code.
            await _content.EnsureAccessAsync(app, depot, token);

            var manifestId = await SteamVerbSupport.ResolveManifestAsync(_content, _logger, app, depot, manifest);

            var files = (await _content.ListFilesAsync(app, depot, manifestId, token))
                .Where(f => string.IsNullOrWhiteSpace(filter) ||
                            f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var lines = files.Select(f => $"{f.Size,14}  {f.Sha1}  {f.Path}").ToArray();

            if (output != default)
            {
                await output.WriteAllLinesAsync(lines, token);
                _logger.LogInformation("Wrote {Count} entries to {Output}", files.Length, output);
            }
            else
            {
                foreach (var line in lines) Console.WriteLine(line);
                _logger.LogInformation("{Count} entries", files.Length);
            }

            return 0;
        }
        catch (Exception ex) when (SteamVerbSupport.Report(_logger, ex) is { } code)
        {
            return code;
        }
    }
}
