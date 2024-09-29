using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compression.BSA;
using Wabbajack.Compression.Zip;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.PHash;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Utilities;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer;

public class StandardInstaller : AInstaller<StandardInstaller>
{

    public StandardInstaller(ILogger<StandardInstaller> logger,
        InstallerConfiguration config,
        IGameLocator gameLocator, FileExtractor.FileExtractor extractor,
        DTOSerializer jsonSerializer, Context vfs, FileHashCache fileHashCache,
        DownloadDispatcher downloadDispatcher, ParallelOptions parallelOptions, IResource<IInstaller> limiter, Client wjClient, IImageLoader imageLoader) :
        base(logger, config, gameLocator, extractor, jsonSerializer, vfs, fileHashCache, downloadDispatcher,
            parallelOptions, limiter, wjClient, imageLoader)
    {
        MaxSteps = 14;
    }

    public static StandardInstaller Create(IServiceProvider provider, InstallerConfiguration configuration)
    {
        return new StandardInstaller(provider.GetRequiredService<ILogger<StandardInstaller>>(),
            configuration,
            provider.GetRequiredService<IGameLocator>(),
            provider.GetRequiredService<FileExtractor.FileExtractor>(),
            provider.GetRequiredService<DTOSerializer>(),
            provider.GetRequiredService<Context>(),
            provider.GetRequiredService<FileHashCache>(),
            provider.GetRequiredService<DownloadDispatcher>(),
            provider.GetRequiredService<ParallelOptions>(),
            provider.GetRequiredService<IResource<IInstaller>>(),
            provider.GetRequiredService<Client>(),
            provider.GetRequiredService<IImageLoader>());
    }

    public override async Task<bool> Begin(CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;
        _logger.LogInformation("Installing: {Name} - {Version}", _configuration.ModList.Name, _configuration.ModList.Version);
        await _wjClient.SendMetric(MetricNames.BeginInstall, ModList.Name);
        NextStep(Consts.StepPreparing, "Configuring Installer", 0);
        _logger.LogInformation("Configuring Processor");

        if (_configuration.GameFolder == default)
            _configuration.GameFolder = _gameLocator.GameLocation(_configuration.Game);

        if (_configuration.GameFolder == default)
        {
            var otherGame = _configuration.Game.MetaData().CommonlyConfusedWith
                .Where(g => _gameLocator.IsInstalled(g)).Select(g => g.MetaData()).FirstOrDefault();
            if (otherGame != null)
                _logger.LogError(
                    "In order to do a proper install Wabbajack needs to know where your {lookingFor} folder resides. However this game doesn't seem to be installed, we did however find an installed " +
                    "copy of {otherGame}, did you install the wrong game?",
                    _configuration.Game.MetaData().HumanFriendlyGameName, otherGame.HumanFriendlyGameName);
            else
                _logger.LogError(
                    "In order to do a proper install Wabbajack needs to know where your {lookingFor} folder resides. However this game doesn't seem to be installed.",
                    _configuration.Game.MetaData().HumanFriendlyGameName);

            return false;
        }

        if (!_configuration.GameFolder.DirectoryExists())
        {
            _logger.LogError("Located game {game} at \"{gameFolder}\" but the folder does not exist!",
                _configuration.Game, _configuration.GameFolder);
            return false;
        }


        _logger.LogInformation("Install Folder: {InstallFolder}", _configuration.Install);
        _logger.LogInformation("Downloads Folder: {DownloadFolder}", _configuration.Downloads);
        _logger.LogInformation("Game Folder: {GameFolder}", _configuration.GameFolder);
        _logger.LogInformation("Wabbajack Folder: {WabbajackFolder}", KnownFolders.EntryPoint);

        _configuration.Install.CreateDirectory();
        _configuration.Downloads.CreateDirectory();

        await OptimizeModlist(token);
        if (token.IsCancellationRequested) return false;

        await HashArchives(token);
        if (token.IsCancellationRequested) return false;

        await DownloadArchives(token);
        if (token.IsCancellationRequested) return false;

        await HashArchives(token);
        if (token.IsCancellationRequested) return false;

        var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
        if (missing.Count > 0)
        {
            if (missing.Any(m => m.State is not Nexus))
            {
                ShowMissingManualReport(missing.Where(m => m.State is not Nexus).ToArray());
                return false;
            }

            foreach (var a in missing)
                _logger.LogCritical("Unable to download {name} ({primaryKeyString})", a.Name,
                    a.State.PrimaryKeyString);
            _logger.LogCritical("Cannot continue, was unable to download one or more archives");
            return false;
        }

        await ExtractModlist(token);
        if (token.IsCancellationRequested) return false;

        await PrimeVFS();

        await BuildFolderStructure();

        await InstallArchives(token);
        if (token.IsCancellationRequested) return false;

        await InstallIncludedFiles(token);
        if (token.IsCancellationRequested) return false;

        await WriteMetaFiles(token);
        if (token.IsCancellationRequested) return false;

        await BuildBSAs(token);
        if (token.IsCancellationRequested) return false;

        // TODO: Port this
        await GenerateZEditMerges(token);
        if (token.IsCancellationRequested) return false;

        await ForcePortable();
        await RemapMO2File();

        CreateOutputMods();

        SetScreenSizeInPrefs();

        await ExtractedModlistFolder!.DisposeAsync();
        await _wjClient.SendMetric(MetricNames.FinishInstall, ModList.Name);

        NextStep(Consts.StepFinished, "Finished", 1);
        _logger.LogInformation("Finished Installation");
        return true;
    }

    private void ShowMissingManualReport(Archive[] toArray)
    {
        _logger.LogError("Writing Manual helper report");
        var report = _configuration.Downloads.Combine("MissingManuals.html");
        {
            using var writer = new StreamWriter(report.Open(FileMode.Create, FileAccess.Write, FileShare.None));
            writer.Write("<html><head><title>Missing Manual Downloads</title></head><body>");
            writer.Write("<h1>Missing Manual Downloads</h1>");
            writer.Write(
                "<p>Wabbajack was unable to download the following archives automatically. Please download them manually and place them in the downloads folder you chose during the install setup.</p>");
            foreach (var archive in toArray)
            {
                switch (archive.State)
                {
                    case Manual manual:
                        writer.Write($"<h3>{archive.Name}</h1>");
                        writer.Write($"<p>{manual.Prompt}</p>");
                        writer.Write($"<p>Download URL: <a href=\"{manual.Url}\">{manual.Url}</a></p>");
                        break;
                    case MediaFire mediaFire:
                        writer.Write($"<h3>{archive.Name}</h1>");
                        writer.Write($"<p>Download URL: <a href=\"{mediaFire.Url}\">{mediaFire.Url}</a></p>");
                        break;
                    default:
                        writer.Write($"<h3>{archive.Name}</h1>");
                        writer.Write($"<p>Unknown download type</p>");
                        writer.Write($"<p>Primary Key (may not be helpful): <a href=\"{archive.State.PrimaryKeyString}\">{archive.State.PrimaryKeyString}</a></p>");
                        break;
                }
            }

            writer.Write("</body></html>");
        }
        
        Process.Start(new ProcessStartInfo("cmd.exe", $"start /c \"{report}\"")
        {
            CreateNoWindow = true,
        });
    }

    private Task RemapMO2File()
    {
        var iniFile = _configuration.Install.Combine("ModOrganizer.ini");
        if (!iniFile.FileExists()) return Task.CompletedTask;

        _logger.LogInformation("Remapping ModOrganizer.ini");

        var iniData = iniFile.LoadIniFile();
        var settings = iniData["Settings"];
        settings["download_directory"] = _configuration.Downloads.ToString().Replace("\\", "/");
        iniData.SaveIniFile(iniFile);
        return Task.CompletedTask;
    }

    private void CreateOutputMods()
    {
        // Non MO2 Installs won't have this
        var profileDir = _configuration.Install.Combine("profiles");
        if (!profileDir.DirectoryExists()) return;

        profileDir
            .EnumerateFiles()
            .Where(f => f.FileName == Consts.SettingsIni)
            .Do(f =>
            {
                if (!f.FileExists())
                {
                    _logger.LogInformation("settings.ini is null for {profile}, skipping", f);
                    return;
                }

                var ini = f.LoadIniFile();

                var overwrites = ini["custom_overrides"];
                if (overwrites == null)
                {
                    _logger.LogInformation("No custom overwrites found, skipping");
                    return;
                }

                overwrites!.Do(keyData =>
                {
                    var v = keyData.Value;
                    var mod = _configuration.Install.Combine(Consts.MO2ModFolderName, (RelativePath) v);

                    mod.CreateDirectory();
                });
            });
    }

    private async Task ForcePortable()
    {
        var path = _configuration.Install.Combine("portable.txt");
        if (path.FileExists()) return;

        try
        {
            await path.WriteAllTextAsync("Created by Wabbajack");
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Could not create portable.txt in {_configuration.Install}",
                _configuration.Install);
        }
    }

    private async Task WriteMetaFiles(CancellationToken token)
    {
        _logger.LogInformation("Looking for downloads by size");
        var bySize = UnoptimizedArchives.ToLookup(x => x.Size);

        _logger.LogInformation("Writing Metas");
        await _configuration.Downloads.EnumerateFiles()
            .Where(download => download.Extension != Ext.Meta)
            .PDoAll(async download =>
            {
                var metaFile = download.WithExtension(Ext.Meta);

                var found = bySize[download.Size()];
                Hash hash = default;
                try
                {
                    hash = await FileHashCache.FileHashCachedAsync(download, token);
                }
                catch(Exception ex)
                {
                    _logger.LogError($"Failed to get hash for file {download}!");
                    throw;
                }
                var archive = found.FirstOrDefault(f => f.Hash == hash);

                IEnumerable<string> meta;

                if (archive == default)
                {
                    // archive is not part of the Modlist

                    if (metaFile.FileExists())
                    {
                        try
                        {
                            var parsed = metaFile.LoadIniFile();
                            if (parsed["General"] is not null && (
                                    parsed["General"]["removed"] is null ||
                                    parsed["General"]["removed"].Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase)))
                            {
                                // add removed=true to files not part of the Modlist so they don't show up in MO2
                                parsed["General"]["removed"] = "true";

                                _logger.LogInformation("Writing {FileName}", metaFile.FileName);
                                parsed.SaveIniFile(metaFile);
                            }
                        }
                        catch (Exception)
                        {
                            return;
                        }

                        return;
                    }

                    // create new meta file if missing
                    meta = new[]
                    {
                        "[General]",
                        "removed=true"
                    };
                }
                else
                {
                    if (metaFile.FileExists())
                    {
                        try
                        {
                            var parsed = metaFile.LoadIniFile();
                            if (parsed["General"] is not null && parsed["General"]["unknownArchive"] is null)
                            {
                                // meta doesn't have an associated archive
                                return;
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    meta = AddInstalled(_downloadDispatcher.MetaIni(archive));
                }

                _logger.LogInformation("Writing {FileName}", metaFile.FileName);
                await metaFile.WriteAllLinesAsync(meta, token);
            });
    }

    private static IEnumerable<string> AddInstalled(IEnumerable<string> getMetaIni)
    {
        yield return "[General]";
        yield return "installed=true";

        foreach (var f in getMetaIni)
        {
            yield return f;
        }
    }

    private async Task BuildBSAs(CancellationToken token)
    {
        var bsas = ModList.Directives.OfType<CreateBSA>().ToList();
        _logger.LogInformation("Generating debug caches");
        var indexedByDestination = UnoptimizedDirectives.ToDictionary(d => d.To);
        _logger.LogInformation("Building {bsasCount} bsa files", bsas.Count);
        NextStep("Installing", "Building BSAs", bsas.Count);

        foreach (var bsa in bsas)
        {
            UpdateProgress(1);
            _logger.LogInformation("Building {bsaTo}", bsa.To.FileName);
            var sourceDir = _configuration.Install.Combine(Consts.BSACreationDir, bsa.TempID);

            await using var a = BSADispatch.CreateBuilder(bsa.State, _manager);
            var streams = await bsa.FileStates.PMapAllBatchedAsync(_limiter, async state =>
            {
                var fs = sourceDir.Combine(state.Path).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                await a.AddFile(state, fs, token);
                return fs;
            }).ToList();

            _logger.LogInformation("Writing {bsaTo}", bsa.To);
            var outPath = _configuration.Install.Combine(bsa.To);

            await using (var outStream = outPath.Open(FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await a.Build(outStream, token);
            }

            streams.Do(s => s.Dispose());

            await FileHashCache.FileHashWriteCache(outPath, bsa.Hash);
            sourceDir.DeleteDirectory();

            _logger.LogInformation("Verifying {bsaTo}", bsa.To);
            var reader = await BSADispatch.Open(outPath);
            var results = await reader.Files.PMapAllBatchedAsync(_limiter, async state =>
            {
                var sf = await state.GetStreamFactory(token);
                await using var stream = await sf.GetStream();
                var hash = await stream.Hash(token);

                var astate = bsa.FileStates.First(f => f.Path == state.Path);
                var srcDirective = indexedByDestination[Consts.BSACreationDir.Combine(bsa.TempID, astate.Path)];
                //DX10Files are lossy
                if (astate is not BA2DX10File && srcDirective.IsDeterministic)
                    ThrowOnNonMatchingHash(bsa, srcDirective, astate, hash);
                return (srcDirective, hash);
            }).ToHashSet();
        }

        var bsaDir = _configuration.Install.Combine(Consts.BSACreationDir);
        if (bsaDir.DirectoryExists())
        {
            _logger.LogInformation("Removing temp folder {bsaCreationDir}", Consts.BSACreationDir);
            bsaDir.DeleteDirectory();
        }
    }

    private async Task InstallIncludedFiles(CancellationToken token)
    {
        _logger.LogInformation("Writing inline files");
        NextStep(Consts.StepInstalling, "Installing Included Files", ModList.Directives.OfType<InlineFile>().Count());
        await ModList.Directives
            .OfType<InlineFile>()
            .PDoAll(async directive =>
            {
                UpdateProgress(1);
                var outPath = _configuration.Install.Combine(directive.To);
                outPath.Delete();

                switch (directive)
                {
                    case RemappedInlineFile file:
                        await WriteRemappedFile(file);
                        await FileHashCache.FileHashCachedAsync(outPath, token);
                        break;
                    default:
                        var hash = await outPath.WriteAllHashedAsync(await LoadBytesFromPath(directive.SourceDataID), token);
                        if (!Consts.KnownModifiedFiles.Contains(directive.To.FileName))
                            ThrowOnNonMatchingHash(directive, hash);

                        await FileHashCache.FileHashWriteCache(outPath, directive.Hash);
                        break;
                }
            });
    }

    private void SetScreenSizeInPrefs()
    {
        var profilesPath = _configuration.Install.Combine("profiles");

        // Don't remap files for Native Game Compiler games
        if (!profilesPath.DirectoryExists()) return;
        if (_configuration.SystemParameters == null)
            _logger.LogWarning("No SystemParameters set, ignoring ini settings for system parameters");

        var config = new IniParserConfiguration {AllowDuplicateKeys = true, AllowDuplicateSections = true};
        config.CommentRegex = new Regex(@"^(#|;)(.*)");
        var oblivionPath = (RelativePath) "Oblivion.ini";

        if (profilesPath.DirectoryExists())
        {
            foreach (var file in profilesPath.EnumerateFiles()
                         .Where(f => ((string) f.FileName).EndsWith("refs.ini") || f.FileName == oblivionPath))
                try
                {
                    var parser = new FileIniDataParser(new IniDataParser(config));
                    var data = parser.ReadFile(file.ToString());
                    var modified = false;
                    if (data.Sections["Display"] != null)
                        if (data.Sections["Display"]["iSize W"] != null && data.Sections["Display"]["iSize H"] != null)
                        {
                            data.Sections["Display"]["iSize W"] =
                                _configuration.SystemParameters!.ScreenWidth.ToString(CultureInfo.CurrentCulture);
                            data.Sections["Display"]["iSize H"] =
                                _configuration.SystemParameters.ScreenHeight.ToString(CultureInfo.CurrentCulture);
                            modified = true;
                        }

                    if (data.Sections["MEMORY"] != null)
                        if (data.Sections["MEMORY"]["VideoMemorySizeMb"] != null)
                        {
                            data.Sections["MEMORY"]["VideoMemorySizeMb"] =
                                _configuration.SystemParameters!.EnbLEVRAMSize.ToString(CultureInfo.CurrentCulture);
                            modified = true;
                        }

                    if (!modified) continue;
                    parser.WriteFile(file.ToString(), data);
                    _logger.LogTrace("Remapped screen size in {file}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Skipping screen size remap for {file} due to parse error.", file);
                }
        }

        var tweaksPath = (RelativePath) "SSEDisplayTweaks.ini";
        foreach (var file in _configuration.Install.EnumerateFiles()
            .Where(f => f.FileName == tweaksPath))
            try
            {
                var parser = new FileIniDataParser(new IniDataParser(config));
                var data = parser.ReadFile(file.ToString());
                var modified = false;
                if (data.Sections["Render"] != null)
                    if (data.Sections["Render"]["Resolution"] != null)
                    {
                        data.Sections["Render"]["Resolution"] =
                            $"{_configuration.SystemParameters!.ScreenWidth.ToString(CultureInfo.CurrentCulture)}x{_configuration.SystemParameters.ScreenHeight.ToString(CultureInfo.CurrentCulture)}";
                        modified = true;
                    }

                if (modified)
                    parser.WriteFile(file.ToString(), data);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Skipping screen size remap for {file} due to parse error.", file);
            }

        // The Witcher 3
        if (_configuration.Game == Game.Witcher3)
        {
            var name = (RelativePath)"user.settings";
            foreach (var file in _configuration.Install.Combine("profiles").EnumerateFiles()
                         .Where(f => f.FileName == name))
            {
                try
                {
                    var parser = new FileIniDataParser(new IniDataParser(config));
                    var data = parser.ReadFile(file.ToString());
                    data["Viewport"]["Resolution"] =
                        $"{_configuration.SystemParameters!.ScreenWidth}x{_configuration.SystemParameters!.ScreenHeight}";
                    parser.WriteFile(file.ToString(), data);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "While remapping user.settings");
                }
            }
        }
    }

    private async Task WriteRemappedFile(RemappedInlineFile directive)
    {
        var data = Encoding.UTF8.GetString(await LoadBytesFromPath(directive.SourceDataID));

        var gameFolder = _configuration.GameFolder.ToString();

        data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, gameFolder);
        data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, gameFolder.Replace("\\", "\\\\"));
        data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, gameFolder.Replace("\\", "/"));

        data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, _configuration.Install.ToString());
        data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK,
            _configuration.Install.ToString().Replace("\\", "\\\\"));
        data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, _configuration.Install.ToString().Replace("\\", "/"));

        data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, _configuration.Downloads.ToString());
        data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK,
            _configuration.Downloads.ToString().Replace("\\", "\\\\"));
        data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD,
            _configuration.Downloads.ToString().Replace("\\", "/"));

        await _configuration.Install.Combine(directive.To).WriteAllTextAsync(data);
    }

    public async Task GenerateZEditMerges(CancellationToken token)
    {
        var patches = _configuration.ModList
            .Directives
            .OfType<MergedPatch>()
            .ToList();
        NextStep("Installing", "Generating ZEdit Merges", patches.Count);

        await patches.PMapAllBatchedAsync(_limiter, async m =>
        {
            UpdateProgress(1);
            _logger.LogInformation("Generating zEdit merge: {to}", m.To);

            var srcData = (await m.Sources.SelectAsync(async s =>
                        await _configuration.Install.Combine(s.RelativePath).ReadAllBytesAsync(token))
                    .ToReadOnlyCollection())
                .ConcatArrays();

            var patchData = await LoadBytesFromPath(m.PatchID);

            await using var fs = _configuration.Install.Combine(m.To)
                .Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            try
            {
                var hash = await BinaryPatching.ApplyPatch(new MemoryStream(srcData), new MemoryStream(patchData), fs);
                ThrowOnNonMatchingHash(m, hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While creating zEdit merge, entering debugging mode");
                foreach (var source in m.Sources)
                {
                    var hash = await _configuration.Install.Combine(source.RelativePath).Hash();
                    _logger.LogInformation("For {Source} expected hash {Expected} got {Got}", source.RelativePath, source.Hash, hash);
                }

                throw;
            }

            return m;
        }).ToList();
    }

    public static async Task<ModList> Load(DTOSerializer dtos, DownloadDispatcher dispatcher, ModlistMetadata metadata, CancellationToken token)
    {
        var archive = new Archive
        {
            State = dispatcher.Parse(new Uri(metadata.Links.Download))!,
            Size = metadata.DownloadMetadata!.Size,
            Hash = metadata.DownloadMetadata.Hash
        };

        await using var stream = await dispatcher.ChunkedSeekableStream(archive, token);
        await using var reader = new ZipReader(stream);
        var entry = (await reader.GetFiles()).First(e => e.FileName == "modlist");
        using var ms = new MemoryStream();
        await reader.Extract(entry, ms, token);
        ms.Position = 0;
        return JsonSerializer.Deserialize<ModList>(ms, dtos.Options)!;
    }
}
