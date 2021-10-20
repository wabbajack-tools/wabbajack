using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.PHash;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Utilities;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer
{
    public record StatusUpdate(string StatusText, Percent StepsProgress, Percent StepProgress) {}
    
    public abstract class AInstaller<T>
        where T : AInstaller<T>
    {
        private static readonly Regex NoDeleteRegex = new(@"(?i)[\\\/]\[NoDelete\]", RegexOptions.Compiled);

        protected readonly InstallerConfiguration _configuration;
        protected readonly DownloadDispatcher _downloadDispatcher;
        private readonly FileExtractor.FileExtractor _extractor;
        private readonly FileHashCache _fileHashCache;
        protected readonly IGameLocator _gameLocator;
        private readonly DTOSerializer _jsonSerializer;
        protected readonly ParallelOptions _parallelOptions;
        protected readonly ILogger<T> _logger;
        protected readonly TemporaryFileManager _manager;
        private readonly Context _vfs;
        protected readonly Client _wjClient;
        
       

        private long _maxStepProgress = 0;
        private int _currentStep = 0;
        private string _statusText;
        private long _currentStepProgress;
        private Stopwatch _updateStopWatch = new();

        protected long MaxSteps { get; set; }

        public event EventHandler<StatusUpdate> OnStatusUpdate; 



        public AInstaller(ILogger<T> logger, InstallerConfiguration config, IGameLocator gameLocator,
            FileExtractor.FileExtractor extractor,
            DTOSerializer jsonSerializer, Context vfs, FileHashCache fileHashCache,
            DownloadDispatcher downloadDispatcher,
            ParallelOptions parallelOptions, Client wjClient)
        {
            _manager = new TemporaryFileManager(config.Install.Combine("__temp__"));
            ExtractedModlistFolder = _manager.CreateFolder();
            _configuration = config;
            _logger = logger;
            _extractor = extractor;
            _jsonSerializer = jsonSerializer;
            _vfs = vfs;
            _fileHashCache = fileHashCache;
            _downloadDispatcher = downloadDispatcher;
            _parallelOptions = parallelOptions;
            _gameLocator = gameLocator;
            _wjClient = wjClient;
        }

        public void NextStep(string statusText, long maxStepProgress)
        {
            _updateStopWatch.Restart();
            _maxStepProgress = maxStepProgress;
            _currentStep += 1;
            _statusText = statusText;
            _logger.LogInformation("Next Step: {Step}", statusText);

            if (OnStatusUpdate != null)
            {
                OnStatusUpdate(this, new StatusUpdate($"[{_currentStep}/{MaxSteps}] " + statusText, Percent.FactoryPutInRange(_currentStep, MaxSteps),
                    Percent.Zero));
            }
        }

        public void UpdateProgress(long stepProgress)
        {
            Interlocked.Add(ref _currentStepProgress, stepProgress);

            lock (_updateStopWatch)
            {
                if (_updateStopWatch.ElapsedMilliseconds < 100) return;
                _updateStopWatch.Restart();
            }

            if (OnStatusUpdate != null)
            {
                OnStatusUpdate(this, new StatusUpdate(_statusText, Percent.FactoryPutInRange(_currentStep, MaxSteps),
                    Percent.FactoryPutInRange(_currentStepProgress, _maxStepProgress)));
            }
        }

        public Dictionary<Hash, AbsolutePath> HashedArchives { get; set; } = new();
        public bool UseCompression { get; set; }

        public TemporaryPath ExtractedModlistFolder { get; set; }

        public ModList ModList => _configuration.ModList;

        public abstract Task<bool> Begin(CancellationToken token);

        public async Task ExtractModlist(CancellationToken token)
        {
            ExtractedModlistFolder = _manager.CreateFolder();
            await _extractor.ExtractAll(_configuration.ModlistArchive, ExtractedModlistFolder, token);
        }

        public async Task<byte[]> LoadBytesFromPath(RelativePath path)
        {
            var fullPath = ExtractedModlistFolder.Path.Combine(path);
            if (!fullPath.FileExists())
                throw new Exception($"Cannot load inlined data {path} file does not exist");

            return await fullPath.ReadAllBytesAsync();
        }

        public async Task<Stream> InlinedFileStream(RelativePath inlinedFile)
        {
            var fullPath = ExtractedModlistFolder.Path.Combine(inlinedFile);
            return fullPath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static async Task<ModList> LoadFromFile(DTOSerializer serializer, AbsolutePath path)
        {
            await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            using var ar = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = ar.GetEntry("modlist");
            if (entry == null)
            {
                entry = ar.GetEntry("modlist.json");
                if (entry == null)
                    throw new Exception("Invalid Wabbajack Installer");
                await using var e = entry.Open();
                return (await serializer.DeserializeAsync<ModList>(e))!;
            }

            await using (var e = entry.Open())
            {
                return (await serializer.DeserializeAsync<ModList>(e))!;
            }
        }

        /// <summary>
        ///     We don't want to make the installer index all the archives, that's just a waste of time, so instead
        ///     we'll pass just enough information to VFS to let it know about the files we have.
        /// </summary>
        protected async Task PrimeVFS()
        {
            _vfs.AddKnown(_configuration.ModList.Directives.OfType<FromArchive>().Select(d => d.ArchiveHashPath),
                HashedArchives);
            await _vfs.BackfillMissing();
        }

        public void BuildFolderStructure()
        {
            _logger.LogInformation("Building Folder Structure");
            ModList.Directives
                .Where(d => d.To.Depth > 1)
                .Select(d => _configuration.Install.Combine(d.To.Parent))
                .Distinct()
                .Do(f => f.CreateDirectory());
        }

        public async Task InstallArchives(CancellationToken token)
        {
            NextStep("Installing files", ModList.Directives.Sum(d => d.Size));
            var grouped = ModList.Directives
                .OfType<FromArchive>()
                .Select(a => new { VF = _vfs.Index.FileForArchiveHashPath(a.ArchiveHashPath), Directive = a })
                .GroupBy(a => a.VF)
                .ToDictionary(a => a.Key);

            if (grouped.Count == 0) return;

            await _vfs.Extract(grouped.Keys.ToHashSet(), async (vf, sf) =>
            {
                foreach (var directive in grouped[vf])
                {
                    var file = directive.Directive;
                    UpdateProgress(file.Size);

                    switch (file)
                    {
                        case PatchedFromArchive pfa:
                        {
                            await using var s = await sf.GetStream();
                            s.Position = 0;
                            await using var patchDataStream = await InlinedFileStream(pfa.PatchID);
                            var toFile = file.To.RelativeTo(_configuration.Install);
                            {
                                await using var os = toFile.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                                await BinaryPatching.ApplyPatch(s, patchDataStream, os);
                            }
                        }
                            break;


                        case TransformedTexture tt:
                        {
                            await using var s = await sf.GetStream();
                            await using var of = directive.Directive.To.RelativeTo(_configuration.Install)
                                .Open(FileMode.Create, FileAccess.Write);
                            await ImageLoader.Recompress(s, tt.ImageState.Width, tt.ImageState.Height, tt.ImageState.Format, of, token);
                        }
                            break;


                        case FromArchive _:
                            if (grouped[vf].Count() == 1)
                            {
                                await sf.Move(directive.Directive.To.RelativeTo(_configuration.Install), token);
                            }
                            else
                            {
                                await using var s = await sf.GetStream();
                                await directive.Directive.To.RelativeTo(_configuration.Install)
                                    .WriteAllAsync(s, token, false);
                            }

                            break;
                        default:
                            throw new Exception($"No handler for {directive}");
                    }
                }
            }, token);
        }

        public async Task DownloadArchives(CancellationToken token)
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            _logger.LogInformation("Missing {count} archives", missing.Count);

            var dispatchers = missing.Select(m => _downloadDispatcher.Downloader(m))
                .Distinct()
                .ToList();

            await Task.WhenAll(dispatchers.Select(d => d.Prepare()));

            _logger.LogInformation("Downloading validation data");
            var validationData = await _wjClient.LoadDownloadAllowList();

            _logger.LogInformation("Validating Archives");
            foreach (var archive in missing.Where(archive =>
                !_downloadDispatcher.Downloader(archive).IsAllowed(validationData, archive.State)))
            {
                _logger.LogCritical("File {primaryKeyString} failed validation", archive.State.PrimaryKeyString);
                return;
            }
            
            _logger.LogInformation("Downloading missing archives");
            await DownloadMissingArchives(missing, token);
        }

        public async Task DownloadMissingArchives(List<Archive> missing, CancellationToken token, bool download = true)
        {
            if (download)
            {
                var result = SendDownloadMetrics(missing);
                foreach (var a in missing.Where(a => a.State is Manual))
                {
                    var outputPath = _configuration.Downloads.Combine(a.Name);
                    await _downloadDispatcher.Download(a, outputPath, token);
                }
            }

            _logger.LogInformation("Downloading {count} archives", missing.Count);
            NextStep("Downloading files", missing.Count);

            await missing
                .OrderBy(a => a.Size)
                .Where(a => a.State is not Manual)
                .PDoAll(async archive =>
                {
                    _logger.LogInformation("Downloading {archive}", archive.Name);
                    var outputPath = _configuration.Downloads.Combine(archive.Name);

                    if (download)
                        if (outputPath.FileExists())
                        {
                            var origName = Path.GetFileNameWithoutExtension(archive.Name);
                            var ext = Path.GetExtension(archive.Name);
                            var uniqueKey = archive.State.PrimaryKeyString.StringSha256Hex();
                            outputPath = _configuration.Downloads.Combine(origName + "_" + uniqueKey + "_" + ext);
                            outputPath.Delete();
                        }

                    await DownloadArchive(archive, download, token, outputPath);
                    UpdateProgress(1);
                });
        }

        private async Task SendDownloadMetrics(List<Archive> missing)
        {
            var grouped = missing.GroupBy(m => m.State.GetType());
            foreach (var group in grouped)
                await _wjClient.SendMetric($"downloading_{@group.Key.FullName!.Split(".").Last().Split("+").First()}",
                    @group.Sum(g => (long)g.Size).ToString());
        }

        public async Task<bool> DownloadArchive(Archive archive, bool download, CancellationToken token,
            AbsolutePath? destination = null)
        {
            try
            {
                destination ??= _configuration.Downloads.Combine(archive.Name);

                var (result, hash) =
                    await _downloadDispatcher.DownloadWithPossibleUpgrade(archive, destination.Value, token);

                if (hash != default)
                    _fileHashCache.FileHashWriteCache(destination.Value, hash);

                if (result == DownloadResult.Update)
                    await destination.Value.MoveToAsync(destination.Value.Parent.Combine(archive.Hash.ToHex()), true,
                        token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download error for file {name}", archive.Name);
                return false;
            }

            return false;
        }

        public async Task HashArchives(CancellationToken token)
        {
            _logger.LogInformation("Looking for files to hash");

            var allFiles = _configuration.Downloads.EnumerateFiles()
                .Concat(_gameLocator.GameLocation(_configuration.Game).EnumerateFiles())
                .ToList();

            var hashDict = allFiles.GroupBy(f => f.Size()).ToDictionary(g => g.Key);

            var toHash = ModList.Archives.Where(a => hashDict.ContainsKey((long)a.Size))
                .SelectMany(a => hashDict[(long)a.Size]).ToList();

            _logger.LogInformation("Found {count} total files, {hashedCount} matching filesize", allFiles.Count,
                toHash.Count);

            var hashResults = await
                toHash
                    .PMap(_parallelOptions, async e => (await _fileHashCache.FileHashCachedAsync(e, token), e))
                    .ToList();

            HashedArchives = hashResults
                .OrderByDescending(e => e.Item2.LastModified())
                .GroupBy(e => e.Item1)
                .Select(e => e.First())
                .Where(x => x.Item1 != default)
                .ToDictionary(kv => kv.Item1, kv => kv.e);
        }


        /// <summary>
        ///     The user may already have some files in the _configuration.Install. If so we can go through these and
        ///     figure out which need to be updated, deleted, or left alone
        /// </summary>
        protected async Task OptimizeModlist(CancellationToken token)
        {
            _logger.LogInformation("Optimizing ModList directives");



            var indexed = ModList.Directives.ToDictionary(d => d.To);


            var profileFolder = _configuration.Install.Combine("profiles");
            var savePath = (RelativePath)"saves";

            var existingFiles = _configuration.Install.EnumerateFiles().ToList();
            NextStep("Optimizing Modlist: Looking for files to delete", existingFiles.Count);
            await existingFiles
                .PDo(_parallelOptions, async f =>
                {
                    UpdateProgress(1);
                    var relativeTo = f.RelativeTo(_configuration.Install);
                    if (indexed.ContainsKey(relativeTo) || f.InFolder(_configuration.Downloads))
                        return;

                    if (f.InFolder(profileFolder) && f.Parent.FileName == savePath) return;

                    if (NoDeleteRegex.IsMatch(f.ToString()))
                        return;

                    _logger.LogInformation("Deleting {relativeTo} it's not part of this ModList", relativeTo);
                    f.Delete();
                });

            _logger.LogInformation("Cleaning empty folders");
            NextStep("Optimizing Modlist: Cleaning empty folders", indexed.Keys.Count);
            var expectedFolders = indexed.Keys
                .Select(f => f.RelativeTo(_configuration.Install))
                // We ignore the last part of the path, so we need a dummy file name
                .Append(_configuration.Downloads.Combine("_"))
                .OnEach(_ => UpdateProgress(1))
                .Where(f => f.InFolder(_configuration.Install))
                .SelectMany(path =>
                {
                    // Get all the folders and all the folder parents
                    // so for foo\bar\baz\qux.txt this emits ["foo", "foo\\bar", "foo\\bar\\baz"]
                    var split = ((string)path.RelativeTo(_configuration.Install)).Split('\\');
                    return Enumerable.Range(1, split.Length - 1).Select(t => string.Join("\\", split.Take(t)));
                })
                .Distinct()
                .Select(p => _configuration.Install.Combine(p))
                .ToHashSet();

            try
            {
                var toDelete = _configuration.Install.EnumerateDirectories()
                    .Where(p => !expectedFolders.Contains(p))
                    .OrderByDescending(p => p.ToString().Length)
                    .ToList();
                foreach (var dir in toDelete) dir.DeleteDirectory(true);
            }
            catch (Exception)
            {
                // ignored because it's not worth throwing a fit over
                _logger.LogWarning("Error when trying to clean empty folders. This doesn't really matter.");
            }

            var existingfiles = _configuration.Install.EnumerateFiles().ToHashSet();

            NextStep("Optimizing Modlist: Removing redundant directives", indexed.Count);
            await indexed.Values.PMapAll<Directive, Directive?>(async d =>
                {
                    UpdateProgress(1);
                    // Bit backwards, but we want to return null for 
                    // all files we *want* installed. We return the files
                    // to remove from the install list.
                    var path = _configuration.Install.Combine(d.To);
                    if (!existingfiles.Contains(path)) return null;

                    return await _fileHashCache.FileHashCachedAsync(path, token) == d.Hash ? d : null;
                })
                .Do(d =>
                {
                    if (d != null) indexed.Remove(d.To);
                });

            _logger.LogInformation("Optimized {optimized} directives to {indexed} required", ModList.Directives.Length,
                indexed.Count);
            var requiredArchives = indexed.Values.OfType<FromArchive>()
                .GroupBy(d => d.ArchiveHashPath.Hash)
                .Select(d => d.Key)
                .ToHashSet();

            ModList.Archives = ModList.Archives.Where(a => requiredArchives.Contains(a.Hash)).ToArray();
            ModList.Directives = indexed.Values.ToArray();
        }
    }
}