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
            originalModlistMetadata =
                await _dtos.DeserializeAsync<DownloadMetadata>(File.OpenRead(original + ".meta.json"));
        }
        catch (FileNotFoundException e)
        {
            _logger.LogWarning("Original modlist metadata file could not be found, skipping download/install size changes");
        }

        try
        {
            updatedModlistMetadata =
                await _dtos.DeserializeAsync<DownloadMetadata>(File.OpenRead(updated + ".meta.json"));
        }
        catch (FileNotFoundException e)
        {
            _logger.LogWarning("Updated modlist metadata file could not be found, skipping download/install size changes");
        }
        
        if (!(originalModlist.Version < updatedModlist.Version))
        {
            _logger.LogError("Updated modlist is not newer than the original modlist");
            return -1;
        }
        
        StringBuilder markdownString = new();
        markdownString.AppendLine($"## {updatedModlist.Version}");
        markdownString.AppendLine($"**Build at:** `{File.GetLastWriteTime(updated.ToString())}`");
        markdownString.AppendLine();
        
        if (originalModlistMetadata is not null && updatedModlistMetadata is not null)
        {
            var downloadSizeChange = originalModlistMetadata.SizeOfArchives - updatedModlistMetadata.SizeOfArchives;
            var installSizeChange = originalModlistMetadata.SizeOfInstalledFiles - updatedModlistMetadata.SizeOfInstalledFiles;

            markdownString.AppendLine("**Info:**");
            markdownString.AppendLine($"- Download size change: {downloadSizeChange.ToFileSizeString()} (Total: {updatedModlistMetadata.SizeOfArchives.ToFileSizeString()})");
            markdownString.AppendLine($"- Install size change: {installSizeChange.ToFileSizeString()} (Total: {updatedModlistMetadata.SizeOfInstalledFiles.ToFileSizeString()})");
            markdownString.AppendLine();
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
            markdownString.AppendLine("**Download Changes:**");
            markdownString.AppendLine();
        }

        newArchives.Do(a =>
        {
            markdownString.AppendLine($"- Added [{GetModName(a)}{GetModVersion(a)}]({GetManifestUrl(a)})");
        });
        
        updatedArchives.Do(a =>
        {
            markdownString.AppendLine($"- Updated [{GetModName(a)} to{GetModVersion(a)}]({GetManifestUrl(a)})");
        });
        
        removedArchives.Do(a =>
        {
            markdownString.AppendLine($"- Removed [{GetModName(a)}]({GetManifestUrl(a)})");
        });
        
        // Blank line for presentation
        markdownString.AppendLine();

        var outputFile = Path.Combine(output.ToString(), "changelog.md");

        if (File.Exists(outputFile) && outputFile.EndsWith("md"))
        {
            _logger.LogInformation($"Output file {outputFile} already exists and is a markdown file. It will be updated with the newest version");

            var markdown = (await File.ReadAllLinesAsync(outputFile)).ToList();
            
            if (markdown.Contains(markdownString.ToString()))
            {
                _logger.LogInformation("The output file is already up to date");
                return 0;
            }

            var doc = Markdown.Parse(await File.ReadAllTextAsync(outputFile));

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

            markdown.Insert(markdown.Count, markdownString.ToString());

            await File.WriteAllLinesAsync(outputFile, markdown);

            return 0;
        }

        var text = "# Changelog\n\n" +
        $"- [{updatedModlist.Version}](#{ToTocLink(updatedModlist.Version.ToString())})\n\n" +
        $"{markdownString}";

        await File.WriteAllTextAsync(outputFile, text);
        _logger.LogInformation($"Output file {outputFile} written\n");

        return 0;
    }

    private async Task<string> GetTextFileFromModlist(AbsolutePath archive, ModList modlist, RelativePath sourceId)
    {
        var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
        {
            ModList = modlist,
            ModlistArchive = archive,
        });

        var bytes = await installer.LoadBytesFromPath(sourceId);
        return Encoding.Default.GetString(bytes);
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