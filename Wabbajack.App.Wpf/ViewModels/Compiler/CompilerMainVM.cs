using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Extensions;
using Wabbajack.Installer;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using FileMode = System.IO.FileMode;

namespace Wabbajack;

public class CompilerMainVM : BaseCompilerVM, ICanGetHelpVM, ICpuStatusVM
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly IEnumerable<INeedsLogin> _logins;
    private readonly DownloadDispatcher _downloadDispatcher;

    private readonly ITokenProvider<NexusOAuthState> _nexusTokenProvider;
    private readonly HttpClient _httpClient;

    public CompilerDetailsVM CompilerDetailsVM { get; set; }
    public CompilerFileManagerVM CompilerFileManagerVM { get; set; }

    public LogStream LoggerProvider { get; }
    public CancellationTokenSource CancellationTokenSource { get; private set; }
    public ICommand GetHelpCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand PublishCollectionCommand { get; }

    public ICommand RefreshPreflightChecksCommand { get; }

    [Reactive] public bool IsPublishing { get; set; }
    [Reactive] public bool IsPublishingCollection { get; set; }
    [Reactive] public Percent PublishingPercentage { get; set; } = Percent.One;
    [Reactive] public CompilerState State { get; set; }
    [Reactive] public string BusyStatusText { get; set; } = "";

    [Reactive] public bool? PreflightChecksPassed { get; set; } = null;
    [Reactive] public string PreflightCheckMessage { get; set; } = "";

    [Reactive] public int? ExistingCollectionRevisionNumber { get; set; }
    [Reactive] public string? ExistingCollectionSlug { get; set; }
    [Reactive] public bool IsCheckingCollectionStatus { get; set; }

    [Reactive] public Percent CollectionPublishingPercentage { get; set; } = Percent.One;
    [Reactive] public string CollectionPublishingStage { get; set; } = "";

    public bool IsBusy => IsPublishing || IsPublishingCollection;

    public bool Cancelling { get; private set; }

    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    public enum PublishCollectionResult
    {
        None = 0,
        Success = 1,
        Failed = 2
    }

    [Reactive] public PublishCollectionResult PublishCollectionLastResult { get; set; } = PublishCollectionResult.None;

    public CompilerMainVM(
        ILogger<CompilerMainVM> logger,
        DTOSerializer dtos,
        SettingsManager settingsManager,
        LogStream loggerProvider,
        Client wjClient,
        IServiceProvider serviceProvider,
        ResourceMonitor resourceMonitor,
        CompilerDetailsVM compilerDetailsVM,
        CompilerFileManagerVM compilerFileManagerVM,
        IEnumerable<INeedsLogin> logins,
        DownloadDispatcher downloadDispatcher,
        ITokenProvider<NexusOAuthState> nexusTokenProvider,
        HttpClient httpClient
    ) : base(dtos, settingsManager, logger, wjClient)
    {
        _serviceProvider = serviceProvider;
        _resourceMonitor = resourceMonitor;
        _logins = logins;
        _downloadDispatcher = downloadDispatcher;

        _nexusTokenProvider = nexusTokenProvider;
        _httpClient = httpClient;

        LoggerProvider = loggerProvider;
        CompilerDetailsVM = compilerDetailsVM;
        CompilerFileManagerVM = compilerFileManagerVM;

        GetHelpCommand = ReactiveCommand.Create(GetHelp);

        StartCommand = ReactiveCommand.Create(StartCompilation,
            this.WhenAnyValue(vm => vm.Settings.ModListName,
                              vm => vm.Settings.ModListAuthor,
                              vm => vm.Settings.ModListDescription,
                              vm => vm.Settings.ModListImage,
                              vm => vm.Settings.Downloads,
                              vm => vm.Settings.OutputFile,
                              vm => vm.Settings.Version,
                              (name, author, desc, img, downloads, output, version) =>
                                  !string.IsNullOrWhiteSpace(name) &&
                                  !string.IsNullOrWhiteSpace(author) &&
                                  !string.IsNullOrWhiteSpace(desc) &&
                                  img.FileExists() &&
                                  !string.IsNullOrEmpty(downloads.ToString()) &&
                                  !string.IsNullOrEmpty(output.ToString()) && output.Extension == Ext.Wabbajack &&
                                  Version.TryParse(version, out _)));

        CancelCommand = ReactiveCommand.Create(CancelCompilation);
        OpenLogCommand = ReactiveCommand.Create(OpenLog);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);

        PublishCommand = ReactiveCommand.Create(Publish,
            this.WhenAnyValue(vm => vm.State,
                              vm => vm.IsPublishing,
                              vm => vm.IsPublishingCollection,
                              vm => vm.PreflightChecksPassed,
                              (state, isPublishing, isPublishingCollection, preflightPassed) =>
                                  !isPublishing && !isPublishingCollection &&
                                  state == CompilerState.Completed &&
                                  preflightPassed == true));

        PublishCollectionCommand = ReactiveCommand.CreateFromTask(PublishCollection,
            this.WhenAnyValue(vm => vm.State,
                              vm => vm.IsPublishing,
                              vm => vm.IsPublishingCollection,
                              vm => vm.PreflightChecksPassed,
                              (state, isPublishing, isPublishingCollection, preflightPassed) =>
                                  !isPublishing && !isPublishingCollection &&
                                  state == CompilerState.Completed &&
                                  preflightPassed == true));

        RefreshPreflightChecksCommand = ReactiveCommand.CreateFromTask(async () => await RunPreflightChecksAsync());

        ProgressPercent = Percent.Zero;

        this.WhenActivated(disposables =>
        {
            if (State != CompilerState.Compiling)
            {
                ShowNavigation.Send();
                ConfigurationText = "Modlist Details";
                ProgressText = "Compilation";
                ProgressPercent = Percent.Zero;
                CurrentStep = Step.Configuration;
                State = CompilerState.Configuration;
                ProgressState = ProgressState.Normal;
                // Reset collection publishing state when entering configuration
                CollectionPublishingPercentage = Percent.One;
                CollectionPublishingStage = "";
                PublishCollectionLastResult = PublishCollectionResult.None;
            }

            this.WhenAnyValue(x => x.CompilerDetailsVM.Settings)
                .BindTo(this, x => x.Settings)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.Include)
                .BindTo(this, x => x.Settings.Include)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.Ignore)
                .BindTo(this, x => x.Settings.Ignore)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.NoMatchInclude)
                .BindTo(this, x => x.Settings.NoMatchInclude)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.AlwaysEnabled)
                .BindTo(this, x => x.Settings.AlwaysEnabled)
                .DisposeWith(disposables);
            this.WhenAnyValue(x => x.State)
                .Where(s => s == CompilerState.Completed)
                .Subscribe(async _ =>
                {
                    await RunPreflightChecksAsync();
                    await CheckExistingCollectionStatus();
                    // Reset collection publishing state when compilation completes
                    CollectionPublishingPercentage = Percent.One;
                    CollectionPublishingStage = "";
                    PublishCollectionLastResult = PublishCollectionResult.None;
                })
                .DisposeWith(disposables);

        });
    }

    private void OpenLog()
    {
        var log = KnownFolders.LauncherAwarePath.Combine("logs").Combine("Wabbajack.current.log").ToString();
        Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
    }

    private async Task Publish()
    {
        try
        {
            BusyStatusText = "Publishing modlist...";
            IsPublishing = true;

            PublishingPercentage = Percent.Zero;


            var downloadMetadata = _dtos.Deserialize<DownloadMetadata>(
                await Settings.OutputFile.WithExtension(Ext.Meta).WithExtension(Ext.Json).ReadAllTextAsync())!;
            var (progress, publishTask) = await _wjClient.PublishModlist(
                Settings.MachineUrl,
                Version.Parse(Settings.Version),
                Settings.OutputFile,
                downloadMetadata);

            using var progressSubscription = progress.Subscribe(p => PublishingPercentage = p.PercentDone);
            await publishTask;
        }
        catch (Exception ex)
        {
            _logger.LogError("While publishing: {ex}", ex);
        }
        finally
        {
            IsPublishing = false;
            PublishingPercentage = Percent.One;
            BusyStatusText = "";
        }
    }

    private void OpenFolder() => UIUtils.OpenFolderAndSelectFile(Settings.OutputFile);

    private void GetHelp() => Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html") { UseShellExecute = true });

    private async Task StartCompilation()
    {
        var tsk = Task.Run(async () =>
        {
            try
            {
                HideNavigation.Send();
                await SaveSettings();

                await EnsureLoggedIntoNexus();

                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ProgressText = "Compiling...";
                    State = CompilerState.Compiling;
                    CurrentStep = Step.Busy;
                    ProgressText = "Compiling...";
                    ProgressState = ProgressState.Normal;
                    return Disposable.Empty;
                });

                Settings.UseGamePaths = true;

                var compiler = MO2Compiler.Create(_serviceProvider, Settings.ToCompilerSettings());

                var events = Observable.FromEventPattern<StatusUpdate>(h => compiler.OnStatusUpdate += h,
                        h => compiler.OnStatusUpdate -= h)
                    .ObserveOnGuiThread()
                    .Subscribe(update =>
                    {
                        RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                        {
                            var s = update.EventArgs;
                            ProgressText = $"{s.StatusText}";
                            ProgressPercent = s.StepsProgress;
                            return Disposable.Empty;
                        });
                    });

                try
                {
                    CancellationTokenSource = new CancellationTokenSource();
                    bool result = await compiler.Begin(CancellationTokenSource.Token);
                    if (!result)
                        throw new Exception("Compilation Failed");
                }
                finally
                {
                    events.Dispose();
                    CancellationTokenSource.Dispose();
                }

                _logger.LogInformation("Compiler Finished");

                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ShowNavigation.Send();
                    ProgressText = "Compiled";
                    ProgressPercent = Percent.One;
                    State = CompilerState.Completed;
                    CurrentStep = Step.Done;
                    ProgressState = ProgressState.Success;

                    // Reset collection publishing state for fresh start
                    CollectionPublishingPercentage = Percent.One;
                    CollectionPublishingStage = "";
                    PublishCollectionLastResult = PublishCollectionResult.None;

                    return Disposable.Empty;
                });
            }
            catch (Exception ex)
            {
                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ShowNavigation.Send();
                    if (Cancelling)
                    {
                        this.ProgressText = "Compilation Cancelled";
                        ProgressPercent = Percent.One;
                        State = CompilerState.Configuration;
                        _logger.LogInformation(ex, "Cancelled compilation: {Message}", ex.Message);
                        Cancelling = false;

                        // Reset collection publishing state
                        CollectionPublishingPercentage = Percent.One;
                        CollectionPublishingStage = "";
                        PublishCollectionLastResult = PublishCollectionResult.None;

                        return Disposable.Empty;
                    }
                    else
                    {
                        this.ProgressText = "Compilation Failed";
                        ProgressPercent = Percent.Zero;

                        State = CompilerState.Errored;
                        _logger.LogError(ex, "Failed compilation: {Message}", ex.Message);

                        // Reset collection publishing state
                        CollectionPublishingPercentage = Percent.One;
                        CollectionPublishingStage = "";
                        PublishCollectionLastResult = PublishCollectionResult.None;

                        return Disposable.Empty;
                    }
                });
            }
        });

        await tsk;
    }

    private async Task RunPreflightChecksAsync()
    {
        
        try
        {
            _logger.LogInformation("Running preflight checks...");
            PreflightCheckMessage = "Running checks...";
            PreflightChecksPassed = null;

            var passed = await RunPreflightChecks(CancellationToken.None);

            PreflightChecksPassed = passed;
            PreflightCheckMessage = passed
                ? "Ready to publish"
                : $"Checks failed: List '{Settings.MachineUrl}' not found in repository or invalid version";

            _logger.LogInformation("Preflight checks {status}", passed ? "passed" : "failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running preflight checks");
            PreflightChecksPassed = false;
            PreflightCheckMessage = "Checks failed: " + ex.Message;
        }
    }

    private async Task PublishCollection()
    {
        try
        {
            BusyStatusText = "Preparing collection upload...";
            PublishCollectionLastResult = PublishCollectionResult.None;
            IsPublishingCollection = true;
            CollectionPublishingPercentage = Percent.One;
            CollectionPublishingStage = "Reading modlist...";

            ModList modList;

            await using (var fs = Settings.OutputFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var za = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var entry = za.GetEntry("modlist");
                if (entry == null)
                {
                    _logger.LogError("Cannot publish collection: 'modlist' entry not found in {output}", Settings.OutputFile);
                    PublishCollectionLastResult = PublishCollectionResult.Failed;
                    return;
                }

                using var es = entry.Open();
                using var sr = new StreamReader(es);
                var modListJson = await sr.ReadToEndAsync();
                modList = _dtos.Deserialize<ModList>(modListJson)!;
            }

            CollectionPublishingPercentage = new Percent(0.05);

            // Get the game version if the game is installed
            string? gameVersion = null;
            try
            {
                CollectionPublishingStage = "Detecting game version...";
                var gameLocator = _serviceProvider.GetRequiredService<IGameLocator>();
                if (gameLocator.TryFindLocation(modList.GameType, out var gamePath))
                {
                    var mainFile = modList.GameType.MetaData().MainExecutable!.Value.RelativeTo(gamePath);
                    if (mainFile.FileExists())
                    {
                        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(mainFile.ToString());
                        gameVersion = versionInfo.FileVersion;
                        _logger.LogInformation("Detected game version: {version} for {game}", gameVersion, modList.GameType);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not detect game version, collection will be created without game version requirement");
            }

            CollectionPublishingPercentage = new Percent(0.1);
            CollectionPublishingStage = "Converting to collection format...";
            _logger.LogInformation(
                "Building Vortex collection from {count} archives...",
                modList.Archives?.Count() ?? 0);

            var vortexJson = WabbajackToVortexCollection.Serialize(modList, gameVersion);

            _logger.LogInformation("Vortex collection built successfully");
            var collectionJsonPath = Settings.OutputFile.WithExtension(new Extension(".collection.json"));

            await collectionJsonPath.WriteAllTextAsync(vortexJson);

            var uploader = new NexusCollectionUploader(
                _logger,
                _nexusTokenProvider,
                _httpClient,
                _dtos.Options
            );

            // Subscribe to progress events
            uploader.OnProgress += (stage, progress) =>
            {
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    switch (stage)
                    {
                        case "requesting_url":
                            CollectionPublishingStage = "Requesting upload URL...";
                            CollectionPublishingPercentage = new Percent(0.15);
                            break;
                        case "upload":
                            // Map upload progress from 0.15 to 0.85 (70% of total)
                            var uploadPercent = 0.15 + (progress * 0.70);
                            CollectionPublishingStage = $"Uploading file ({progress:P0})...";
                            CollectionPublishingPercentage = new Percent(uploadPercent);
                            BusyStatusText = $"Uploading to Nexus Mods ({progress:P0})...";
                            break;
                        case "building_manifest":
                            CollectionPublishingStage = "Building manifest...";
                            CollectionPublishingPercentage = new Percent(0.86);
                            BusyStatusText = "Building collection manifest...";
                            break;
                        case "sending_manifest":
                            CollectionPublishingStage = "Sending to Nexus Mods...";
                            CollectionPublishingPercentage = new Percent(0.90);
                            BusyStatusText = "Sending manifest to Nexus Mods (this may take several minutes)...";
                            break;
                        case "finalizing":
                            CollectionPublishingStage = "Finalizing...";
                            CollectionPublishingPercentage = new Percent(0.95);
                            BusyStatusText = "Finalizing collection...";
                            break;
                        case "complete":
                            CollectionPublishingStage = "Complete!";
                            CollectionPublishingPercentage = Percent.One;
                            break;
                    }
                });
            };

            var listDomain = WabbajackToVortexCollection.GetDomain(modList.GameType.ToString());

            // Load stored mapping from the author's modlists.json
            int? existingCollectionId = null;
            string? existingSlug = null;
            string? existingDomain = null;

            try
            {
                var mapping = await _wjClient.GetNexusCollectionMapping(Settings.MachineUrl, CancellationToken.None);
                if (mapping != null && mapping.CollectionId > 0)
                {
                    existingCollectionId = mapping.CollectionId;
                    existingSlug = mapping.Slug;
                    existingDomain = mapping.DomainName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Nexus collection mapping from modlists.json; will create a new collection instead.");
            }

            if (existingCollectionId.HasValue &&
                !string.IsNullOrWhiteSpace(existingDomain) &&
                !existingDomain.Equals(listDomain, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Stored Nexus mapping domain '{stored}' does not match current list domain '{current}'. Ignoring stored collectionId={id}.",
                    existingDomain, listDomain, existingCollectionId.Value);

                existingCollectionId = null;
                existingSlug = null;
                existingDomain = null;
            }

            if (existingCollectionId.HasValue)
                _logger.LogInformation("Using stored Nexus collection mapping: collectionId={id} slug={slug} domain={domain}",
                    existingCollectionId.Value, existingSlug, existingDomain);
            else
                _logger.LogInformation("No stored Nexus collection mapping found; creating new collection.");

            var result = await uploader.UploadCollection(
                modList,
                collectionJsonPath,
                Settings.OutputFile,
                existingCollectionId: existingCollectionId,
                gameVersion: gameVersion,
                CancellationToken.None);

            if (result != null && result.Success)
            {
                PublishCollectionLastResult = PublishCollectionResult.Success;

                // put mapping back to the author modlists.json
                try
                {
                    await _wjClient.SetNexusCollectionMapping(
                        Settings.MachineUrl,
                        result.CollectionId,
                        result.Slug,
                        listDomain,
                        result.RevisionNumber,
                        CancellationToken.None);

                    _logger.LogInformation("Persisted Nexus mapping to modlists.json: collectionId={id} slug={slug} domain={domain} rev={rev}",
                        result.CollectionId, result.Slug, listDomain, result.RevisionNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Collection created/updated, but failed to persist Nexus mapping into modlists.json");
                }

                try
                {
                    var url = $"https://www.nexusmods.com/games/{listDomain}/collections/{result.Slug}/revisions/{result.RevisionNumber}";
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Exception openEx)
                {
                    _logger.LogWarning(openEx, "Failed to open browser for uploaded collection");
                }
            }
            else
            {
                PublishCollectionLastResult = PublishCollectionResult.Failed;
                _logger.LogWarning("Collection upload failed.");
            }
        }
        catch (Exception ex)
        {
            PublishCollectionLastResult = PublishCollectionResult.Failed;
            _logger.LogWarning(ex, "Failed to upload collection to Nexus Mods, this is optional and won't affect your Wabbajack modlist");
        }
        finally
        {
            IsPublishingCollection = false;
            CollectionPublishingPercentage = Percent.One;
            BusyStatusText = "";
        }
    }

    private async Task CheckExistingCollectionStatus()
    {
        try
        {
            IsCheckingCollectionStatus = true;
            ExistingCollectionRevisionNumber = null;
            ExistingCollectionSlug = null;

            // Get mapping from modlists.json
            var mapping = await _wjClient.GetNexusCollectionMapping(Settings.MachineUrl, CancellationToken.None);
            if (mapping == null || mapping.CollectionId <= 0 || string.IsNullOrWhiteSpace(mapping.Slug))
            {
                _logger.LogInformation("No existing Nexus collection mapping found");
                return;
            }

            ExistingCollectionSlug = mapping.Slug;

            // Use the Game from Settings to get the domain
            var listDomain = WabbajackToVortexCollection.GetDomain(Settings.Game.ToString());

            // Now query Nexus GraphQL to get the latest revision number
            var latestRevision = await GetLatestCollectionRevision(
                mapping.Slug,
                mapping.DomainName ?? listDomain,
                CancellationToken.None);

            if (latestRevision.HasValue)
            {
                ExistingCollectionRevisionNumber = latestRevision.Value;
                _logger.LogInformation("Found existing collection '{slug}' at revision {rev}",
                    mapping.Slug, latestRevision.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check existing collection status");
            ExistingCollectionRevisionNumber = null;
            ExistingCollectionSlug = null;
        }
        finally
        {
            IsCheckingCollectionStatus = false;
        }
    }

    private async Task<int?> GetLatestCollectionRevision(string slug, string domainName, CancellationToken token)
    {
        if (!_nexusTokenProvider.HaveToken())
            return null;

        var authState = await _nexusTokenProvider.Get();
        if (authState?.OAuth?.IsExpired ?? true)
            return null;

        var query = @"
            query collectionRevision($slug: String!, $domainName: String!) {
              collectionRevision(slug: $slug, domainName: $domainName) {
                revisionNumber
              }
            }";

        var variables = new
        {
            slug,
            domainName
        };

        var graphqlRequest = new
        {
            query,
            variables
        };

        using var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(graphqlRequest, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.nexusmods.com/v2/graphql")
        {
            Content = content
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authState.OAuth.AccessToken);
        request.Headers.TryAddWithoutValidation("Application-Name", "Wabbajack");
        request.Headers.TryAddWithoutValidation("Application-Version", "0.0.0");
        request.Headers.TryAddWithoutValidation("Protocol-Version", "1.5.0");

        try
        {
            var response = await _httpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch collection revision: {status}", response.StatusCode);
                return null;
            }

            var root = System.Text.Json.Nodes.JsonNode.Parse(responseBody) as System.Text.Json.Nodes.JsonObject;
            var revisionNumber = root?["data"]?["collectionRevision"]?["revisionNumber"]?.GetValue<int>();

            return revisionNumber;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching collection revision from Nexus");
            return null;
        }
    }

    private async Task EnsureLoggedIntoNexus()
    {
        var nexusDownloadState = new Nexus();
        foreach (var downloader in await _downloadDispatcher.AllDownloaders([nexusDownloadState]))
        {
            _logger.LogInformation("Preparing {Name}", downloader.GetType().Name);
            if (await downloader.Prepare())
                continue;

            var manager = _logins.FirstOrDefault(l => l.LoginFor() == downloader.GetType());
            if(manager == null)
            {
                _logger.LogError("Cannot install, could not prepare {Name} for downloading", downloader.GetType().Name);
                throw new Exception($"No way to prepare {downloader}");
            }

            RxApp.MainThreadScheduler.Schedule(manager, (_, _) =>
            {
                manager.TriggerLogin.Execute(null);
                return Disposable.Empty;
            });

            while (true)
            {
                if (await downloader.Prepare())
                    break;
                await Task.Delay(1000);
            }
        }
    }

    private async Task CancelCompilation()
    {
        if (State != CompilerState.Compiling) return;
        Cancelling = true;
        _logger.LogInformation("Cancel pressed, cancelling compilation...");
        try
        {
            await CancellationTokenSource.CancelAsync();
        }
        catch(ObjectDisposedException ex)
        {
            _logger.LogError("Could not cancel compilation, cancellation token was disposed! Exception: {ex}", ex.ToString());
        }
    }

    private async Task<bool> RunPreflightChecks(CancellationToken token)
    {

        IReadOnlyList<string> lists;
        try
        {
            lists = await _wjClient.GetMyModlists(token);
            _logger.LogInformation("Preflight: Retrieved {Count} modlists from server", lists.Count);
            foreach (var list in lists)
            {
                _logger.LogInformation("Preflight: Found list: '{List}'", list);
            }
            _logger.LogInformation("Preflight: Looking for MachineUrl: '{MachineUrl}'", Settings.MachineUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError("Publish failed; failed to get modlists! Exception: {ex}", ex.ToString());
            return false;
        }

        var found = lists.Any(x => x.Equals(Settings.MachineUrl, StringComparison.InvariantCultureIgnoreCase));
        _logger.LogInformation("Preflight: Match found: {Found}", found);

        if (!found)
        {
            _logger.LogError("Preflight Check failed, list {MachineUrl} not found in any repository", Settings.MachineUrl);
            return false;
        }

        if (!System.Version.TryParse(Settings.Version, out var version))
        {
            _logger.LogError("Preflight Check failed, version {Version} was not valid", Settings.Version);
            return false;
        }

        return true;
    }
}