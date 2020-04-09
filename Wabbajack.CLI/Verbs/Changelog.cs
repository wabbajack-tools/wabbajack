using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Markdig;
using Markdig.Syntax;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("changelog", HelpText = "Generate a changelog using two different versions of the same Modlist.")]
    public class Changelog : AVerb
    {
        [IsFile(CustomMessage = "Modlist %1 does not exist!", Extension = Consts.ModListExtensionString)]
        [Option("original", Required = true, HelpText = "The original/previous modlist")]
        public string Original { get; set; } = "";

        [IsFile(CustomMessage = "Modlist %1 does not exist!", Extension = Consts.ModListExtensionString)]
        [Option("update", Required = true, HelpText = "The current/updated modlist")]
        public string Update { get; set; } = "";

        [Option('o', "output", Required = false, HelpText = "The output file")]
        public string? Output { get; set; }
        
        [Option("changes-downloads", Required = false, Default = true, HelpText = "Include download changes")]
        public bool IncludeDownloadChanges { get; set; }
       
        [Option("changes-mods", Required = false, Default = false, HelpText = "Include mods changes")]
        public bool IncludeModChanges { get; set; }
        
        [Option("changes-loadorder", Required = false, Default = false, HelpText = "Include load order changes")]
        public bool IncludeLoadOrderChanges { get; set; }

        protected override async Task<ExitCode> Run()
        {
            var orignalPath = (AbsolutePath)Original;
            var updatePath = (AbsolutePath)Update;
            if (Original == null)
                return ExitCode.BadArguments;
            if (Update == null)
                return ExitCode.BadArguments;

            ModList original, update;

            try
            {
                original = AInstaller.LoadFromFile(orignalPath);
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error while loading the original Modlist from {Original}!\n{e}", ExitCode.Error);
            }

            if(original == null)
                return CLIUtils.Exit($"The Modlist from {Original} could not be loaded!", ExitCode.Error);

            try
            {
                update = AInstaller.LoadFromFile(updatePath);
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error while loading the updated Modlist from {Update}!\n{e}", ExitCode.Error);
            }

            if(update == null)
                return CLIUtils.Exit($"The Modlist from {Update} could not be loaded!", ExitCode.Error);

            var downloadSizeChanges = original.DownloadSize - update.DownloadSize;
            var installSizeChanges = original.InstallSize - update.InstallSize;

            var versionRegex = new Regex(@"\s([0-9](\.|\s)?){1,4}");
            var matchOriginal = versionRegex.Match(original.Name);
            var matchUpdated = versionRegex.Match(update.Name);

            if (!matchOriginal.Success || !matchUpdated.Success)
            {
                return CLIUtils.Exit(
                    !matchOriginal.Success
                        ? "The name of the original modlist did not match the version check regex!"
                        : "The name of the updated modlist did not match the version check regex!", ExitCode.Error);
            }

            var version = matchUpdated.Value.Trim();

            var mdText =
                $"## {version}\n\n" +
                $"**Build at:** `{File.GetCreationTime(Update)}`\n\n" +
                "**Info**:\n\n" +
                $"- Download Size change: {downloadSizeChanges.ToFileSizeString()} (Total: {update.DownloadSize.ToFileSizeString()})\n" +
                $"- Install Size change: {installSizeChanges.ToFileSizeString()} (Total: {update.InstallSize.ToFileSizeString()})\n\n";

            if (IncludeDownloadChanges)
            {
                var updatedArchives = update.Archives
                    .Where(a => original.Archives.All(x => x.Name != a.Name))
                    .Where(a =>
                    {
                        if (!(a.State is NexusDownloader.State nexusState))
                            return false;

                        return original.Archives.Any(x =>
                        {
                            if (!(x.State is NexusDownloader.State originalState))
                                return false;

                            if (nexusState.Name != originalState.Name)
                                return false;

                            if (nexusState.ModID != originalState.ModID)
                                return false;


                            return nexusState.FileID > originalState.FileID;
                        });
                    }).ToList();

                var newArchives = update.Archives
                    .Where(a => original.Archives.All(x => x.Name != a.Name))
                    .Where(a => updatedArchives.All(x => x != a))
                    .ToList();

                var removedArchives = original.Archives
                    .Where(a => update.Archives.All(x => x.Name != a.Name))
                    .Where(a => updatedArchives.All(x => x != a))
                    .ToList();

                if(newArchives.Any() || removedArchives.Any())
                    mdText += "**Download Changes**:\n\n";

                updatedArchives.Do(a =>
                {
                    mdText += $"- Updated [{GetModName(a)}]({a.State.GetManifestURL(a)})\n";
                });

                removedArchives.Do(a =>
                {
                    mdText += $"- Removed [{GetModName(a)}]({a.State.GetManifestURL(a)})\n";
                });

                newArchives.Do(a =>
                {
                    mdText += $"- Added [{GetModName(a)}]({a.State.GetManifestURL(a)})\n";
                });

                mdText += "\n";
            }

            if (IncludeLoadOrderChanges)
            {
                var loadorder_txt = (RelativePath)"loadorder.txt";
                var originalLoadOrderFile = original.Directives
                    .Where(d => d is InlineFile)
                    .Where(d => d.To.FileName == loadorder_txt)
                    .Cast<InlineFile>()
                    .First();

                var updatedLoadOrderFile = update.Directives
                    .Where(d => d is InlineFile)
                    .Where(d => d.To.FileName == loadorder_txt)
                    .Cast<InlineFile>()
                    .First();

                var originalLoadOrder = GetTextFileFromModlist(orignalPath, original, originalLoadOrderFile.SourceDataID).Result.Split("\n");
                var updatedLoadOrder = GetTextFileFromModlist(updatePath, update, updatedLoadOrderFile.SourceDataID).Result.Split("\n");
                
                var addedPlugins = updatedLoadOrder
                    .Where(p => originalLoadOrder.All(x => p != x))
                    .ToList();

                var removedPlugins = originalLoadOrder
                    .Where(p => updatedLoadOrder.All(x => p != x))
                    .ToList();

                if(addedPlugins.Any() || removedPlugins.Any())
                    mdText += "**Load Order Changes**:\n\n";

                addedPlugins.Do(p =>
                {
                    mdText += $"- Added {p}\n";
                });

                removedPlugins.Do(p =>
                {
                    mdText += $"- Removed {p}\n";
                });
                
                mdText += "\n";
            }

            if (IncludeModChanges)
            {
                var modlistTxt = (RelativePath)"modlist.txt";
                var originalModlistFile = original.Directives
                    .Where(d => d is InlineFile)
                    .Where(d => d.To.FileName == modlistTxt)
                    .Cast<InlineFile>()
                    .First();

                var updatedModlistFile = update.Directives
                    .Where(d => d is InlineFile)
                    .Where(d => d.To.FileName == modlistTxt)
                    .Cast<InlineFile>()
                    .First();

                var originalModlist = GetTextFileFromModlist(orignalPath, original, originalModlistFile.SourceDataID).Result.Split("\n");
                var updatedModlist = GetTextFileFromModlist(updatePath, update, updatedModlistFile.SourceDataID).Result.Split("\n");

                var removedMods = originalModlist
                    .Where(m => m.StartsWith("+"))
                    .Where(m => updatedModlist.All(x => m != x))
                    .Select(m => m.Substring(1))
                    .ToList();

                var addedMods = updatedModlist
                    .Where(m => m.StartsWith("+"))
                    .Where(m => originalModlist.All(x => m != x))
                    .Select(m => m.Substring(1))
                    .ToList();

                if (removedMods.Any() || addedMods.Any())
                    mdText += "**Mod Changes**:\n\n";

                addedMods.Do(m =>
                {
                    mdText += $"- Added {m}\n";
                });

                removedMods.Do(m =>
                {
                    mdText += $"- Removed {m}\n";
                });
            }

            var output = string.IsNullOrWhiteSpace(Output)
                ? "changelog.md"
                : Output;

            if (File.Exists(output) && output.EndsWith("md"))
            {
                CLIUtils.Log($"Output file {output} already exists and is a markdown file. It will be updated with the newest version");

                var markdown = File.ReadAllLines(output).ToList();
                var lines = mdText.Split("\n");

                if (lines.All(l => markdown.Contains(l)))
                {
                    return CLIUtils.Exit("The output file is already up-to-date", ExitCode.Ok);
                }

                var doc = Markdown.Parse(File.ReadAllText(output));

                var hasToc = false;
                var tocLine = 0;

                var headers = doc
                    .Where(b => b is HeadingBlock)
                    .Cast<HeadingBlock>()
                    .ToList();

                if (headers.Count < 2)
                {
                    return CLIUtils.Exit("The provided output file has less than 2 headers!", ExitCode.Error);
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

                            CLIUtils.Log($"Toc found at {tocLine}");
                        }
                    }
                }

                var firstHeader = headers
                    .First(h => h.Level >= 2);

                var line = firstHeader.Line-1;

                if (hasToc)
                {
                    markdown.Insert(tocLine, $"- [{version}](#{ToTocLink(version)})");
                    line++;
                }

                markdown.InsertRange(line+1, lines);

                File.WriteAllLines(output, markdown);
                CLIUtils.Log($"Wrote {markdown.Count} lines to {output}");

                return ExitCode.Ok;
            }

            var text = "# Changelog\n\n" +
                       $"- [{version}](#{ToTocLink(version)})\n\n" +
                       $"{mdText}";

            File.WriteAllText(output, text);
            CLIUtils.Log($"Wrote changelog to {output}");

            return ExitCode.Ok;
        }

        private static async Task<string> GetTextFileFromModlist(AbsolutePath archive, ModList modlist, RelativePath sourceID)
        {
            var installer = new MO2Installer(archive, modlist, default, default, null);
            byte[] bytes = await installer.LoadBytesFromPath(sourceID);
            return Encoding.Default.GetString(bytes);
        }

        private static string ToTocLink(string header)
        {
            return header.Trim().Replace(" ", "").Replace(".", "");
        }

        private static string GetModName(Archive a)
        {
            var result = a.Name;

            if (a.State is IMetaState metaState)
            {
                result = metaState.Name;
            }

            return result;
        }
    }
}
