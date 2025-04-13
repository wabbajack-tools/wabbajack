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

        ModList OriginalModlist, UpdatedModlist;

        try
        {
            OriginalModlist = await StandardInstaller.LoadFromFile(_dtos, original);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load original Wabbajack file");
            return -1;
        }

        try
        {
            UpdatedModlist = await StandardInstaller.LoadFromFile(_dtos, updated);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load updated Wabbajack file");
            return -1;
        }

        // iAmMe: download & install size data not exposed in WJ 4.0. It's present in the *.wabbajack.meta file
        // but this file isn't guaranteed to be present. We'll check for it and if it's not there, skip reporting
        // the changes.

        try
        {
            var OriginalModlistMetadata =
                await _dtos.DeserializeAsync<DownloadMetadata>(File.OpenRead(original + ".meta.json"));
        }
        catch (FileNotFoundException e)
        {
            _logger.LogWarning("Original modlist metadata file could not be found, skipping download/install size changes");
        }

        try
        {
            var UpdatedModlistMetadata =
                await _dtos.DeserializeAsync<DownloadMetadata>(File.OpenRead(updated + ".meta.json"));
        }
        catch (FileNotFoundException e)
        {
            _logger.LogWarning("Updated modlist metadata file could not be found, skipping download/install size changes");
        }
        
        if (!(OriginalModlist.Version < UpdatedModlist.Version))
        {
            _logger.LogError("Updated modlist is not newer than the original modlist");
            return -1;
        }

        var MarkdownText =
            $"## {UpdatedModlist.Version}\n\n" +
            $"**Build at:** `{File.GetLastWriteTime(updated.ToString())}`\n\n";

                // iAmMe: download & install size data not exposed in WJ 4.0.

                //"**Info**:\n\n" +
                //$"- Download Size change: {downloadSizeChanges.ToFileSizeString()} (Total: {update.DownloadSize.ToFileSizeString()})\n" +
                //$"- Install Size change: {installSizeChanges.ToFileSizeString()} (Total: {update.InstallSize.ToFileSizeString()})\n\n";

        #region Download Changes

        var UpdatedArchives = UpdatedModlist.Archives
            .Where(a => OriginalModlist.Archives.All(x => x.Name != a.Name))
            .Where(a =>
            {
                if (a.State is not Nexus NexusState)
                    return false;

                return OriginalModlist.Archives.Any(x =>
                {
                    if (x.State is not Nexus OriginalState)
                        return false;

                    if (NexusState.Name != OriginalState.Name)
                        return false;

                    if (NexusState.ModID != OriginalState.ModID)
                        return false;

                    if (!long.TryParse(NexusState.FileID.ToString(), out var CurrentFileId))
                        return true;

                    if (!long.TryParse(OriginalState.FileID.ToString(), out var OriginalFileId))
                    {
                        return CurrentFileId > OriginalFileId;
                    }

                    return true;
                });
            }).ToList();

        var NewArchives = UpdatedModlist.Archives
            .Where(a => OriginalModlist.Archives.All(x => x.Name != a.Name))
            .Where(a => UpdatedArchives.All(x => x != a))
            .ToList();
        
        var RemovedArchives = OriginalModlist.Archives
            .Where(a => UpdatedModlist.Archives.All(x => x.Name != a.Name))
            .Where(a => UpdatedArchives.All(x => x != a))
            .ToList();

        if (NewArchives.Count != 0 || RemovedArchives.Count != 0)
            MarkdownText += "**Download Changes**:\n\n";

        NewArchives.Do(a =>
        {
            MarkdownText += $"- Added [{GetModName(a)}{GetModVersion(a)}]({GetManifestURL(a)})\n";
        });
        
        UpdatedArchives.Do(a =>
        {
            MarkdownText += $"- Updated [{GetModName(a)} to{GetModVersion(a)}]({GetManifestURL(a)})\n";
        });
        
        RemovedArchives.Do(a =>
        {
            MarkdownText += $"- Removed [{GetModName(a)}]({GetManifestURL(a)})\n";
        });

        MarkdownText += "\n";

        #endregion

        #region Load Order Changes
        // Not implemented. Need to find a way of getting modlist.txt from an installed modlist using the AInstaller/StandardInstaller LoadBytesFromPath method.
        /*
        var OriginalLoadOrderFile = OriginalModlist.Directives
            .Where(d => d is InlineFile)
            .Where(d => d.To.EndsWith("loadorder.txt"))
            .Cast<InlineFile>()
            .First();
        
        var UpdatedLoadOrderFile = UpdatedModlist.Directives
            .Where(d => d is InlineFile)
            .Where(d => d.To.EndsWith("loadorder.txt"))
            .Cast<InlineFile>()
            .First();

        var OriginalLoadOrder = GetTextFileFromModlist(original, OriginalModlist, OriginalLoadOrderFile.SourceDataID).ToString();
        
        var UpdatedLoadOrder = GetTextFileFromModlist(updated, UpdatedModlist, UpdatedLoadOrderFile.SourceDataID).ToString();

        var AddedPlugins = UpdatedLoadOrder
            .Where(p => OriginalLoadOrder.All(x => p != x))
            .ToList();
        
        var RemovedPlugins = OriginalLoadOrder
            .Where(p => UpdatedLoadOrder.All(x => p != x))
            .ToList();
        
        if (AddedPlugins.Count != 0 || RemovedPlugins.Count != 0)
            MarkdownText += "**Load Order Changes**:\n\n";

        AddedPlugins.Do(p =>
        {
            MarkdownText += $"- Added {p}\n";
        });
        
        RemovedPlugins.Do(p =>
        {
            MarkdownText += $"- Removed {p}\n";
        });
        
        MarkdownText += "\n";
        */
        #endregion

        #region Mod Changes
        // Not implemented. Need to find a way of getting modlist.txt from an installed modlist using the AInstaller/StandardInstaller LoadBytesFromPath method.
        #endregion

        // TODO: add better error checking here
        if (output == AbsolutePath.Empty)
        {
            _logger.LogError("Updated modlist is not newer than the original modlist");
            return -1;
        }

        var Output = Path.Combine(output.ToString(), "changelog.md");

        if (File.Exists(Output) && Output.EndsWith("md"))
        {
            _logger.LogInformation($"Output file {Output} already exists and is a markdown file. It will be updated with the newest version");

            var markdown = (await File.ReadAllLinesAsync(Output)).ToList();
            var lines = MarkdownText.Split("\n");

            if (lines.All(markdown.Contains))
            {
                _logger.LogInformation("The output file is already up to date");
                return 0;
            }

            var doc = Markdown.Parse(await File.ReadAllTextAsync(Output));

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
                markdown.Insert(tocLine, $"- [{UpdatedModlist.Version}](#{ToTocLink(UpdatedModlist.Version.ToString())})");
                line++;
            }

            markdown.InsertRange(line + 1, lines);

            await File.WriteAllLinesAsync(Output, markdown);

            return 0;
        }

        var text = "# Changelog\n\n" +
        $"- [{UpdatedModlist.Version}](#{ToTocLink(UpdatedModlist.Version.ToString())})\n\n" +
        $"{MarkdownText}";

        await File.WriteAllTextAsync(Output, text);
        _logger.LogInformation($"Output file {Output} written\n");

        return 0;
    }

    private async Task<string> GetTextFileFromModlist(AbsolutePath archive, ModList modlist, RelativePath sourceId)
    {
        var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
        {
            ModList = modlist,
            ModlistArchive = archive,
        });

        return Encoding.Default.GetString(await installer.LoadBytesFromPath(sourceId));
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

    private static Uri? GetManifestURL(Archive a)
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