using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Markdig;
using Markdig.Syntax;
using Wabbajack.Installer;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Paths.IO;
using Wabbajack.DTOs.Directives;
using System.IO.Compression;
using System.Threading;

namespace Wabbajack.CLI.Verbs;

public class Changelog
{
    private readonly ILogger<Changelog> _logger;
    private readonly DTOSerializer _dtos;
    private readonly IServiceProvider _serviceProvider;

    public Changelog(ILogger<Changelog> logger, DTOSerializer dtos, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
    }

    public static VerbDefinition Definition = new("changelog",
        "Generates a changelog, formatted in Markdown, when given 2 Wabbajack files.",
        [
            new OptionDefinition(typeof(AbsolutePath), "or", "original", "Original Wabbajack file"),
            new OptionDefinition(typeof(AbsolutePath), "u", "updated", "Updated Wabbajack file"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output path for the Changelog file")
        ]);

    internal async Task<int> Run(AbsolutePath original, AbsolutePath updated, AbsolutePath output)
    {
        _logger.LogInformation("Loading modlists...");
        
        // TODO: add better handling of the file path checks
        if (original == AbsolutePath.Empty)
        {
            _logger.LogError("Original Wabbajack file not specified or path is incorrect/inaccessible");
            return -1;
        }

        if (updated == AbsolutePath.Empty)
        {
            _logger.LogError("Original Wabbajack file not specified or path is incorrect/inaccessible");
            return -1;
        }

        ModList originalModlist, updatedModlist;

        try
        {
            originalModlist = await StandardInstaller.LoadFromFile(_dtos, original);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load original Wabbajack file");
            return -1;
        }

        try
        {
            updatedModlist = await StandardInstaller.LoadFromFile(_dtos, updated);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load updated Wabbajack file");
            return -1;
        }

        // iAmMe: download and install size data not exposed directly in WJ 4.0. It's present in the *.wabbajack.meta.json
        // file but this file isn't guaranteed to be present. We'll check for it and if it's not there, skip reporting
        // the changes.

        DownloadMetadata? originalModlistMetadata = null;
        DownloadMetadata? updatedModlistMetadata = null;

        try
        {
            using var stream = File.OpenRead(original + ".meta.json");
            originalModlistMetadata = await _dtos.DeserializeAsync<DownloadMetadata>(stream);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Original modlist metadata file could not be found, skipping download/install size changes");
        }

        try
        {
            using var stream = File.OpenRead(updated + ".meta.json");
            updatedModlistMetadata = await _dtos.DeserializeAsync<DownloadMetadata>(stream);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Updated modlist metadata file could not be found, skipping download/install size changes");
        }
        
        if (!(originalModlist.Version < updatedModlist.Version))
        {
            _logger.LogError("Updated modlist is not newer than the original modlist");
            return -1;
        }
        
        StringBuilder mdBuilder = new();
        mdBuilder.AppendLine($"## {updatedModlist.Version}");
        mdBuilder.AppendLine($"**Built at:** `{File.GetLastWriteTime(updated.ToString())}`");
        mdBuilder.AppendLine();
        
        if (originalModlistMetadata is not null && updatedModlistMetadata is not null)
        {
            var downloadSizeChange = originalModlistMetadata.SizeOfArchives - updatedModlistMetadata.SizeOfArchives;
            var installSizeChange = originalModlistMetadata.SizeOfInstalledFiles - updatedModlistMetadata.SizeOfInstalledFiles;

            mdBuilder.AppendLine("**Info:**");
            mdBuilder.AppendLine($"- Download size change: {downloadSizeChange.ToFileSizeString()} (Total: {updatedModlistMetadata.SizeOfArchives.ToFileSizeString()})");
            mdBuilder.AppendLine($"- Install size change: {installSizeChange.ToFileSizeString()} (Total: {updatedModlistMetadata.SizeOfInstalledFiles.ToFileSizeString()})");
            mdBuilder.AppendLine();
        }

        var updatedArchives = updatedModlist.Archives
            .Where(a => originalModlist.Archives.All(x => x.Name != a.Name))
            .Where(a =>
            {
                if (a.State is not Nexus nexusState)
                    return false;

                return originalModlist.Archives.Any(x =>
                {
                    if (x.State is not Nexus originalState)
                        return false;

                    if (nexusState.Name != originalState.Name)
                        return false;

                    if (nexusState.ModID != originalState.ModID)
                        return false;

                    if (!long.TryParse(nexusState.FileID.ToString(), out var currentFileId))
                        return true;

                    if (!long.TryParse(originalState.FileID.ToString(), out var originalFileId))
                    {
                        return currentFileId > originalFileId;
                    }

                    return true;
                });
            }).ToList();

        var newArchives = updatedModlist.Archives
            .Where(a => originalModlist.Archives.All(x => x.Name != a.Name))
            .Where(a => updatedArchives.All(x => x != a))
            .ToList();
        
        var removedArchives = originalModlist.Archives
            .Where(a => updatedModlist.Archives.All(x => x.Name != a.Name))
            .Where(a => updatedArchives.All(x => x != a))
            .ToList();

        if (newArchives.Count != 0 || removedArchives.Count != 0)
        {
            mdBuilder.AppendLine("**Download Changes:**");
            mdBuilder.AppendLine();
        }

        newArchives.Do(a =>
        {
            mdBuilder.AppendLine($"- Added [{GetModName(a)}{GetModVersion(a)}]({GetManifestUrl(a)})");
        });
        
        updatedArchives.Do(a =>
        {
            mdBuilder.AppendLine($"- Updated [{GetModName(a)} to{GetModVersion(a)}]({GetManifestUrl(a)})");
        });
        
        removedArchives.Do(a =>
        {
            mdBuilder.AppendLine($"- Removed [{GetModName(a)}]({GetManifestUrl(a)})");
        });

        // Blank line for presentation
        mdBuilder.AppendLine();
        
        var originalLoadOrderFile = originalModlist.Directives
            .OfType<InlineFile>()
            .Where(d => d.To.EndsWith("loadorder.txt"))
            .FirstOrDefault();
        
        var updatedLoadOrderFile = updatedModlist.Directives
            .OfType<InlineFile>()
            .Where(d => d.To.EndsWith("loadorder.txt"))
            .FirstOrDefault();

        // Make sure to only compare the load order files if they are found, not all games will have a load order
        if (originalLoadOrderFile != default && updatedLoadOrderFile != default)
        {

            using var originalLoadOrderStream = await GetInlinedFileStreamAsync(original, originalLoadOrderFile.SourceDataID);
            var originalLoadOrder = await ReadStreamToStringAsync(originalLoadOrderStream);

            using var updatedLoadOrderStream = await GetInlinedFileStreamAsync(updated, updatedLoadOrderFile.SourceDataID);
            var updatedLoadOrder = await ReadStreamToStringAsync(updatedLoadOrderStream);

            /*
            var addedPlugins = updatedLoadOrder
                .Where(p => originalLoadOrder.All(x => p != x))
                .ToList();

            var removedPlugins = originalLoadOrder
                .Where(p => updatedLoadOrder.All(x => p != x))
                .ToList();

            if (addedPlugins.Count != 0 || removedPlugins.Count != 0)
                mdBuilder.AppendLine("** Load Order Changes:**");

            addedPlugins.Do(p =>
            {
                mdBuilder.Append($"- Added {p}");
            });

            removedPlugins.Do(p =>
            {
                mdBuilder.Append($"- Removed {p}");
            });
            */
        }

        var outputFile = output.Combine("changelog.md");

        if (outputFile.FileExists())
        {
            _logger.LogInformation($"Output file {outputFile} already exists and is a markdown file. It will be updated with the newest version");

            var markdown = await outputFile.ReadAllTextAsync();
            
            var doc = Markdown.Parse(markdown);

            var hasToc = false;
            var tocLine = 0;

            var headers = doc
                .Where(b => b is HeadingBlock)
                .Cast<HeadingBlock>()
                .ToList();

            if (headers.Count < 2)
            {
                _logger.LogError("The provided output file has less than 2 headers");
                return -1;
            }

            if (headers[0].Level == 1 && headers[1].Level == 2)
            {
                if (headers[1].Line - headers[0].Line > headers.Count - 1)
                {
                    var listBlocks = doc
                        .Where(b => b.Line > headers[0].Line && b.Line < headers[1].Line)
                        .OfType<ListBlock>()
                        .ToList();

                    if (listBlocks.Count == 1)
                    {
                        hasToc = true;
                        tocLine = listBlocks[0].Line;
                    }
                }
            }

            var firstHeader = headers
                .First(h => h.Level >= 2);

            var line = firstHeader.Line - 1;

            if (hasToc)
            {
                markdown.Insert(tocLine, $"- [{updatedModlist.Version}](#{ToTocLink(updatedModlist.Version.ToString())})");
                line++;
            }

            await outputFile.WriteAllTextAsync(markdown);

            return 0;
        }

        var text = "# Changelog\n\n" +
        $"- [{updatedModlist.Version}](#{ToTocLink(updatedModlist.Version.ToString())})\n\n" +
        $"{mdBuilder}";

        await outputFile.WriteAllTextAsync(text);
        _logger.LogInformation($"Output file {outputFile} written\n");

        return 0;
    }

    private async Task<Stream?> GetInlinedFileStreamAsync(AbsolutePath modlistFile, RelativePath sourceId)
    {
        await using var stream = modlistFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(e => e.FullName == sourceId.ToString());
        if (entry == null)
            return null;

        MemoryStream ms = new();
        await entry.Open().CopyToAsync(ms);
        return ms;
    }

    private async Task<string> ReadStreamToStringAsync(Stream stream, CancellationToken? token = null)
    {
        stream.Seek(0, SeekOrigin.Begin);
        string text = string.Empty;
        using (var reader = new StreamReader(stream))
        {
            text = await reader.ReadToEndAsync(token.GetValueOrDefault(CancellationToken.None));
        }
        return text;
    }
    private static string ToTocLink(string header)
    {
        return header.Trim().Replace(" ", "").Replace(".", "");
    }

    private static string? GetModName(Archive a)
    {
        var result = a.Name;

        if (a.State is IMetaState metaState)
        {
            result = metaState.Name;
        }

        return result;
    }

    private static Uri? GetManifestUrl(Archive a)
    {
        var defaultUri = new Uri("about:blank");

        return a.State switch
        {
            IMetaState metaState => metaState.LinkUrl,
            Manual manual => manual.Url,
            Http http => http.Url,
            Mega mega => mega.Url,
            MediaFire mediaFire => mediaFire.Url,
            _ => defaultUri
        };
    }

    private static string? GetModVersion(Archive a)
    {
        var result = string.Empty;
        
        if (a.State is IMetaState metaState)
            result = metaState.Version;
        
        if (string.IsNullOrEmpty(result))
            return null;
        else
            return " v" + result;
    }
}