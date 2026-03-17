using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;
using System.Windows;

namespace Wabbajack;
public class ModListGalleryVM : BackNavigatingVM, ICanLoadLocalFileVM
{
    public class GameTypeEntry
    {
        public GameTypeEntry(GameMetaData gameMetaData, int amount)
        {
            GameMetaData = gameMetaData;
            IsAllGamesEntry = gameMetaData == null;
            GameIdentifier = IsAllGamesEntry ? ALL_GAME_IDENTIFIER : gameMetaData?.HumanFriendlyGameName;
            Amount = amount;
            FormattedName = IsAllGamesEntry ? $"{ALL_GAME_IDENTIFIER} ({Amount})" : $"{gameMetaData.HumanFriendlyGameName} ({Amount})";
        }

        public bool IsAllGamesEntry { get; set; }
        public GameMetaData GameMetaData { get; private set; }
        public int Amount { get; private set; }
        public string FormattedName { get; private set; }
        public string GameIdentifier { get; private set; }
        public static GameTypeEntry GetAllGamesEntry(int amount) => new(null, amount);
    }

    public MainWindowVM MWVM { get; }

    private bool _savingSettings = false;
    private readonly SourceCache<GalleryModListMetadataVM, string> _modLists = new(x => x.Metadata.NamespacedName);
    public ReadOnlyObservableCollection<GalleryModListMetadataVM> _filteredModLists;

    public ReadOnlyObservableCollection<GalleryModListMetadataVM> ModLists => _filteredModLists;

    private const string ALL_GAME_IDENTIFIER = "All games";

    [Reactive] public IValidationResult Error { get; set; }

    [Reactive] public string Search { get; set; }

    [Reactive] public bool OnlyInstalled { get; set; }

    [Reactive] public bool IncludeNSFW { get; set; }

    [Reactive] public bool IncludeUnofficial { get; set; }

    [Reactive] public bool ExcludeMods { get; set; }

    [Reactive] public string GameType { get; set; }
    [Reactive] public double MinModlistSize { get; set; }
    [Reactive] public double MaxModlistSize { get; set; }

    public Dictionary<string, string> CommonlyWrongFormattedTags { get; set; } = new();
    [Reactive] public HashSet<ModListTag> AllTags { get; set; } = new();
    [Reactive] public ObservableCollection<ModListTag> HasTags { get; set; } = new();


    [Reactive] public HashSet<ModListMod> AllMods { get; set; } = new();
    [Reactive] public ObservableCollection<ModListMod> HasMods { get; set; } = new();
    [Reactive] public Dictionary<string, HashSet<string>> ModsPerList { get; set; } = new();

    [Reactive] public GalleryModListMetadataVM SmallestSizedModlist { get; set; }
    [Reactive] public GalleryModListMetadataVM LargestSizedModlist { get; set; }

    [Reactive] public ObservableCollection<GameTypeEntry> GameTypeEntries { get; set; }
    private bool _filteringOnGame;
    private GameTypeEntry _selectedGameTypeEntry = null;

    public GameTypeEntry SelectedGameTypeEntry
    {
        get => _selectedGameTypeEntry;
        set
        {
            RaiseAndSetIfChanged(ref _selectedGameTypeEntry, value ?? GameTypeEntries?.FirstOrDefault(gte => gte.IsAllGamesEntry));
            GameType = _selectedGameTypeEntry?.GameIdentifier;
        }
    }

    private readonly Client _wjClient;
    private readonly ILogger<ModListGalleryVM> _logger;
    private readonly GameLocator _locator;
    private readonly ModListDownloadMaintainer _maintainer;
    private readonly SettingsManager _settingsManager;
    private readonly CancellationToken _cancellationToken;
    private readonly IServiceProvider _serviceProvider;

    private readonly SemaphoreSlim _loadModListsGate = new(1, 1);
    private Task? _loadModListsTask;
    private readonly TaskCompletionSource<bool> _galleryLoadedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Reactive] public bool IsResolvingProtocol { get; set; }
    [Reactive] public string ProtocolStatusText { get; set; }

    private string? _protocolInFlightNamespacedName;

    public ICommand ResetFiltersCommand { get; set; }

    public FilePickerVM LocalFilePicker { get; set; }
    public ICommand LoadLocalFileCommand { get; set; }

    public ModListGalleryVM(ILogger<ModListGalleryVM> logger, Client wjClient, GameLocator locator,
        SettingsManager settingsManager, ModListDownloadMaintainer maintainer, CancellationToken cancellationToken, IServiceProvider serviceProvider)
        : base(logger)
    {
        var searchThrottle = TimeSpan.FromSeconds(0.35);
        _wjClient = wjClient;
        _logger = logger;
        _locator = locator;
        _maintainer = maintainer;
        _settingsManager = settingsManager;
        _cancellationToken = cancellationToken;
        _serviceProvider = serviceProvider;

        LocalFilePicker = new FilePickerVM(this);
        LocalFilePicker.ExistCheckOption = FilePickerVM.CheckOptions.On;
        LocalFilePicker.PathType = FilePickerVM.PathTypeOptions.File;
        LocalFilePicker.Filters.AddRange(new[]
        {
            new CommonFileDialogFilter("Wabbajack Modlist", "*" + Ext.Wabbajack),
        });

        ResetFiltersCommand = ReactiveCommand.Create(() => {
            OnlyInstalled = false;
            IncludeNSFW = false;
            IncludeUnofficial = false;
            ExcludeMods = false;
            Search = string.Empty;
            SelectedGameTypeEntry = GameTypeEntries?.FirstOrDefault();
            HasTags = new ObservableCollection<ModListTag>();
            HasMods = new ObservableCollection<ModListMod>();
        });

        LoadLocalFileCommand = ReactiveCommand.Create(() =>
        {
            LocalFilePicker.ConstructTypicalPickerCommand().Execute(null);
            if (LocalFilePicker.TargetPath.FileExists())
            {
                LoadModlistForInstalling.Send(LocalFilePicker.TargetPath, null);
                NavigateToGlobal.Send(ScreenType.Installer);
            }
        });

        this.WhenActivated(disposables =>
        {
            EnsureGalleryLoadedAsync().FireAndForget();
            LoadSettings().FireAndForget();

            if (LoadModlistFromProtocol.TryConsumePending(out var pending))
            {
                _logger.LogInformation("[Protocol] Consumed pending machine URL on activation: {machineUrl}", pending);
                HandleProtocolLoad(pending).FireAndForget();
            }

            MessageBus.Current.Listen<LoadModlistForInstalling>()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(msg =>
                {
                    // If this load is the one initiated via protocol, drop the overlay.
                    var msgMachine = msg.Metadata?.Links?.MachineURL;

                    var msgNamespaced = msg.Metadata?.NamespacedName;
                    if (!string.IsNullOrWhiteSpace(_protocolInFlightNamespacedName) &&
                        !string.IsNullOrWhiteSpace(msgNamespaced) &&
                        msgNamespaced.Equals(_protocolInFlightNamespacedName, StringComparison.OrdinalIgnoreCase))
                    {


                        _logger.LogInformation("[Protocol] LoadModlistForInstalling received for {machineUrl}, clearing overlay", msgMachine);
                        IsResolvingProtocol = false;
                        ProtocolStatusText = string.Empty;
                        _protocolInFlightNamespacedName = null;
                    }
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.IncludeNSFW, x => x.IncludeUnofficial, x => x.OnlyInstalled, x => x.GameType, x => x.ExcludeMods)
                .Subscribe(_ => SaveSettings().FireAndForget())
                .DisposeWith(disposables);


            var searchTextPredicates = this.ObservableForProperty(vm => vm.Search)
                .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                .Select(change => change.Value?.Trim() ?? "")
                .StartWith(Search)
                .Select<string, Func<GalleryModListMetadataVM, bool>>(txt =>
                {
                    if (string.IsNullOrWhiteSpace(txt)) return _ => true;
                    return item => item.Metadata.Title.ContainsCaseInsensitive(txt) ||
                                   item.Metadata.Description.ContainsCaseInsensitive(txt) ||
                                   item.Metadata.Tags.Contains(txt);
                });

            var onlyInstalledGamesFilter = this.ObservableForProperty(vm => vm.OnlyInstalled)
                .Select(v => v.Value)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(onlyInstalled =>
                {
                    if (onlyInstalled == false) return _ => true;
                    return item => _locator.IsInstalled(item.Metadata.Game);
                })
                .StartWith(_ => true);

            var includeUnofficialFilter = this.ObservableForProperty(vm => vm.IncludeUnofficial)
                .Select(v => v.Value)
                .StartWith(IncludeUnofficial)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(unoffical =>
                {
                    if (unoffical) return x => true;
                    return x => x.Metadata.Official;
                });

            var includeNSFWFilter = this.ObservableForProperty(vm => vm.IncludeNSFW)
                .Select(v => v.Value)
                .StartWith(IncludeNSFW)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(showNsfw =>
                {
                    if (showNsfw) return x => true;
                    return x => !x.Metadata.NSFW;
                });

            var gameFilter = this.ObservableForProperty(vm => vm.GameType)
                .Select(v => v.Value)
                .Select<string, Func<GalleryModListMetadataVM, bool>>(selected =>
                {
                    _filteringOnGame = true;
                    if (selected is null or ALL_GAME_IDENTIFIER) return _ => true;
                    return item => item.Metadata.Game.MetaData().HumanFriendlyGameName == selected;
                })
                .StartWith(_ => true);

            var minModlistSizeFilter = this.ObservableForProperty(vm => vm.MinModlistSize)
                                 .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                                 .Select(v => v.Value)
                                 .Select<double, Func<GalleryModListMetadataVM, bool>>(minModlistSize =>
                                 {
                                     return item => item.Metadata.DownloadMetadata.TotalSize >= minModlistSize;
                                 });

            var maxModlistSizeFilter = this.ObservableForProperty(vm => vm.MaxModlistSize)
                                 .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                                 .Select(v => v.Value)
                                 .Select<double, Func<GalleryModListMetadataVM, bool>>(maxModlistSize =>
                                 {
                                     return item => item.Metadata.DownloadMetadata.TotalSize <= maxModlistSize;
                                 });

            var includedTagsFilter = this.ObservableForProperty(vm => vm.HasTags)
                .Select(v => v.Value)
                .Select<ObservableCollection<ModListTag>, Func<GalleryModListMetadataVM, bool>>(filteredTags =>
                {
                    if(!filteredTags?.Any() ?? true) return _ => true;

                    return item => filteredTags.All(tag => item.Metadata.Tags.Contains(tag.Name));
                })
                .StartWith(_ => true);

            var includedModsFilter =
                this.WhenAnyValue(vm => vm.HasMods, vm => vm.ExcludeMods)
                    .Select(tuple => (Mods: tuple.Item1, Exclude: tuple.Item2))
                    .Select(filterData =>
                    {
                        if (!(filterData.Mods?.Any() ?? false)) return (Func<GalleryModListMetadataVM, bool>)(_ => true);

                        if (filterData.Exclude)
                        {
                            // Exclude mode: show modlists that do NOT contain the mods
                            return item =>
                                !ModsPerList.TryGetValue(item.Metadata.Links.MachineURL, out var mods) ||
                                !filterData.Mods.Any(mod => mods.Contains(mod.Name));
                        }

                        // Include mode: show modlists that contain ALL selected mods
                        return item =>
                            ModsPerList.TryGetValue(item.Metadata.Links.MachineURL, out var mods) &&
                            filterData.Mods.All(mod => mods.Contains(mod.Name));
                    })
                    .StartWith(_ => true);


            var searchSorter = this.WhenValueChanged(vm => vm.Search)
                                    .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                                    .Select(s => SortExpressionComparer<GalleryModListMetadataVM>
                                                 .Descending(m => m.Metadata.Title.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase))
                                                 .ThenByDescending(m => m.Metadata.Title.Contains(s ?? "", StringComparison.InvariantCultureIgnoreCase))
                                                 .ThenByDescending(m => !m.IsBroken));
            _modLists.Connect()
                .Filter(searchTextPredicates)
                .Filter(onlyInstalledGamesFilter)
                .Filter(includeUnofficialFilter)
                .Filter(includeNSFWFilter)
                .Filter(gameFilter)
                .Filter(minModlistSizeFilter)
                .Filter(maxModlistSizeFilter)
                .Filter(includedTagsFilter)
                .Filter(includedModsFilter)
                .SortAndBind(out _filteredModLists, searchSorter)
                .Subscribe(_ =>
                {
                    if (!_filteringOnGame)
                    {
                        var previousGameType = GameType;
                        SelectedGameTypeEntry = null;
                        LoadGameTypeEntries();
                        var nextEntry = GameTypeEntries.FirstOrDefault(gte => previousGameType == gte.GameIdentifier);
                        SelectedGameTypeEntry = nextEntry ?? GameTypeEntries.FirstOrDefault(gte => GameType == ALL_GAME_IDENTIFIER);
                    }

                    _filteringOnGame = false;
                })
                .DisposeWith(disposables);
        });
    }

    public override void Unload()
    {
        Error = null;
    }

    private async Task HandleProtocolLoad(string payload)
    {
        var normalized = (payload ?? string.Empty).Trim().Trim('/');

        _protocolInFlightNamespacedName = null;
        IsResolvingProtocol = true;

        using var ll = LoadingLock.WithLoading();

        _logger.LogInformation("[Protocol] Requested load for payload: {payload} (normalized: {normalized})",
            payload, normalized);

        try
        {
            // Check if this is a Nexus Collection
            if (IsNexusCollection(normalized))
            {
                await HandleNexusCollection(normalized);
                ll.Succeed();
                return;
            }

            // Check if this is a direct download URL
            if (IsDirectDownloadUrl(normalized))
            {
                await HandleDirectDownload(normalized);
                ll.Succeed();
                return;
            }

            ProtocolStatusText = $"Preparing to install {normalized}…";

            await EnsureGalleryLoadedAsync();

            GalleryModListMetadataVM? modlist = null;

            if (normalized.Contains("/"))
            {
                // repo/list
                modlist = _modLists.Items.FirstOrDefault(m =>
                    m.Metadata.NamespacedName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Fallback: short list name only
                modlist = _modLists.Items.FirstOrDefault(m =>
                    m.Metadata.Links.MachineURL.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            }

            if (modlist == null)
            {
                _logger.LogWarning("[Protocol] Modlist not found for identifier: {id}", normalized);
                Error = ValidationResult.Fail($"Modlist '{normalized}' not found in gallery");
                IsResolvingProtocol = false;
                ProtocolStatusText = string.Empty;
                ll.Fail();
                return;
            }

            ProtocolStatusText = $"Preparing to install {modlist.Metadata.Title}…";
            _protocolInFlightNamespacedName = modlist.Metadata.NamespacedName;

            _logger.LogInformation("[Protocol] Found modlist '{title}' ({namespaced}), executing InstallCommand",
                modlist.Metadata.Title, modlist.Metadata.NamespacedName);

            // Check if the required game is installed
            if (!_locator.IsInstalled(modlist.Metadata.Game))
            {
                var gameName = modlist.Metadata.Game.MetaData().HumanFriendlyGameName;
                _logger.LogWarning("[Protocol] Cannot install modlist '{title}': Required game '{game}' is not installed",
                    modlist.Metadata.Title, gameName);

                var errorMessage = $"Cannot install '{modlist.Metadata.Title}': {gameName} is not installed on this PC. Please install {gameName} first.";
                Error = ValidationResult.Fail(errorMessage);

                // Show popup error
                MessageBox.Show(
                    errorMessage,
                    "Game Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                IsResolvingProtocol = false;
                ProtocolStatusText = string.Empty;
                ll.Fail();
                return;
            }

            if (!modlist.InstallCommand.CanExecute(null))
            {
                _logger.LogWarning("[Protocol] Cannot install modlist: {name}", modlist.Metadata.Title);
                Error = ValidationResult.Fail($"Modlist '{modlist.Metadata.Title}' cannot be installed at this time");
                IsResolvingProtocol = false;
                ProtocolStatusText = string.Empty;
                ll.Fail();
                return;
            }

            modlist.InstallCommand.Execute(null);
            ll.Succeed();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Protocol] Protocol install cancelled for {id}", normalized);
            Error = ValidationResult.Fail("Loading was cancelled");
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;
            ll.Fail();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Protocol] Failed to load modlist from protocol URL: {id}", normalized);
            Error = ValidationResult.Fail($"Failed to load modlist: {ex.Message}");
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;
            ll.Fail();
        }
    }

    private bool IsDirectDownloadUrl(string payload)
    {
        // ends with .wabbajack?
        return payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               payload.EndsWith(".wabbajack", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNexusCollection(string payload)
    {
        // Check for nexus-collection format: nexus-collection/{slug} or nexus-collection/{slug}/revision/{number}
        return payload.StartsWith("nexus-collection/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleDirectDownload(string downloadUrl)
    {
        _logger.LogInformation("[Protocol] Handling direct download from: {url}", downloadUrl);

        ProtocolStatusText = "Downloading modlist file…";

        try
        {
            // Unescape the URL if needed
            var url = Uri.UnescapeDataString(downloadUrl);

            if (url.StartsWith("http//", StringComparison.OrdinalIgnoreCase))
                url = url.Replace("http//", "http://");
            else if (url.StartsWith("https//", StringComparison.OrdinalIgnoreCase))
                url = url.Replace("https//", "https://");

            _logger.LogInformation("[Protocol] Normalized URL: {url}", url);

            var downloadDir = KnownFolders.EntryPoint.Combine("downloaded_mod_lists");
            if (!downloadDir.DirectoryExists())
                downloadDir.CreateDirectory();

            var fileName = System.IO.Path.GetFileName(new Uri(url).LocalPath);
            if (!fileName.EndsWith(".wabbajack", StringComparison.OrdinalIgnoreCase))
                fileName += ".wabbajack";

            var downloadedFile = downloadDir.Combine(fileName);

            _logger.LogInformation("[Protocol] Downloading to: {path}", downloadedFile);

            // Download the file
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            _logger.LogInformation("[Protocol] File size: {size:F2} MB", totalMB);

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = downloadedFile.Open(System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var lastUpdate = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
                totalRead += bytesRead;

                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 200)
                {
                    if (totalBytes > 0)
                    {
                        var progress = (int)((totalRead * 100) / totalBytes);
                        var downloadedMB = totalRead / (1024.0 * 1024.0);
                        ProtocolStatusText = $"Downloading modlist file… {progress}% ({downloadedMB:F1} / {totalMB:F1} MB)";
                    }
                    else
                    {
                        var downloadedMB = totalRead / (1024.0 * 1024.0);
                        ProtocolStatusText = $"Downloading modlist file… {downloadedMB:F1} MB";
                    }
                    lastUpdate = DateTime.Now;
                }
            }

            await fileStream.FlushAsync(_cancellationToken);

            _logger.LogInformation("[Protocol] Download complete: {path}", downloadedFile);

            ProtocolStatusText = "Preparing modlist…";

            await Task.Delay(500, _cancellationToken);

            ProtocolStatusText = string.Empty;

            _logger.LogInformation("[Protocol] Loading modlist from downloaded file");
            LoadModlistForInstalling.Send(downloadedFile, null);
            NavigateToGlobal.Send(ScreenType.Installer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Protocol] Failed to download modlist from: {url}", downloadUrl);
            Error = ValidationResult.Fail($"Failed to download modlist: {ex.Message}");
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;
            throw;
        }
    }

    private async Task HandleNexusCollection(string payload)
    {
        _logger.LogInformation("[Protocol] Handling Nexus Collection: {payload}", payload);

        ProtocolStatusText = "Fetching collection from Nexus Mods…";

        try
        {
            // Parse the payload: nexus-collection/{slug} or nexus-collection/{slug}/revision/{number}
            var parts = payload.Split('/');

            if (parts.Length < 2)
            {
                _logger.LogError("[Protocol] Invalid Nexus Collection format: {payload}", payload);
                Error = ValidationResult.Fail($"Invalid Nexus Collection URL format: {payload}");
                IsResolvingProtocol = false;
                ProtocolStatusText = string.Empty;
                return;
            }

            var slug = parts[1];
            int? revisionNumber = null;

            // Check if revision number is specified
            if (parts.Length >= 4 && parts[2].Equals("revision", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[3], out var revNum))
                {
                    revisionNumber = revNum;
                }
            }

            _logger.LogInformation("[Protocol] Parsed collection: slug={slug}, revision={revision}",
                slug, revisionNumber?.ToString() ?? "latest");

            // Get the Nexus Collection downloader from DI
            var nexusDownloader = _serviceProvider.GetRequiredService<NexusCollectionDownloader>();

            ProtocolStatusText = $"Fetching {slug} from Nexus Mods…";

            // Get collection download info
            var collectionInfo = await nexusDownloader.GetCollectionDownloadInfo(slug, revisionNumber, _cancellationToken);

            if (collectionInfo == null)
            {
                var errorMessage = !string.IsNullOrWhiteSpace(nexusDownloader.LastError)
                    ? nexusDownloader.LastError
                    : $"Failed to fetch collection '{slug}' from Nexus Mods.";

                _logger.LogError("[Protocol] Failed to get collection info from Nexus");
                Error = ValidationResult.Fail(errorMessage);

                // Show popup error
                MessageBox.Show(
                    errorMessage,
                    "Nexus Collection Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                IsResolvingProtocol = false;
                ProtocolStatusText = string.Empty;
                return;
            }

            _logger.LogInformation("[Protocol] Found collection: {name} (revision {revision})",
                collectionInfo.CollectionName, collectionInfo.RevisionNumber);

            ProtocolStatusText = $"Downloading {collectionInfo.CollectionName}…";

            // Download the .wabbajack file
            var downloadDir = KnownFolders.EntryPoint.Combine("downloaded_mod_lists");
            if (!downloadDir.DirectoryExists())
                downloadDir.CreateDirectory();

            // Create filename from collection info
            var fileName = $"{slug}_r{collectionInfo.RevisionNumber}.wabbajack";
            var downloadedFile = downloadDir.Combine(fileName);

            _logger.LogInformation("[Protocol] Downloading to: {path}", downloadedFile);

            // Download the file from Nexus CDN
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

            using var response = await httpClient.GetAsync(collectionInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            _logger.LogInformation("[Protocol] File size: {size:F2} MB", totalMB);

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = downloadedFile.Open(System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var lastUpdate = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
                totalRead += bytesRead;

                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 200)
                {
                    if (totalBytes > 0)
                    {
                        var progress = (int)((totalRead * 100) / totalBytes);
                        var downloadedMB = totalRead / (1024.0 * 1024.0);
                        ProtocolStatusText = $"Downloading {collectionInfo.CollectionName}… {progress}% ({downloadedMB:F1} / {totalMB:F1} MB)";
                    }
                    else
                    {
                        var downloadedMB = totalRead / (1024.0 * 1024.0);
                        ProtocolStatusText = $"Downloading {collectionInfo.CollectionName}… {downloadedMB:F1} MB";
                    }
                    lastUpdate = DateTime.Now;
                }
            }

            await fileStream.FlushAsync(_cancellationToken);

            _logger.LogInformation("[Protocol] Download complete from Nexus Collection: {path}", downloadedFile);
            _logger.LogInformation("[Protocol] Collection analytics: CollectionId={id}, RevisionId={revId}, Slug={slug}, Revision={rev}",
                collectionInfo.CollectionId, collectionInfo.RevisionId, collectionInfo.CollectionSlug, collectionInfo.RevisionNumber);

            ProtocolStatusText = "Preparing modlist…";

            await Task.Delay(500, _cancellationToken);

            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;

            // Load the modlist as if it were a lcal file
            _logger.LogInformation("[Protocol] Loading modlist from Nexus Collection download");
            LoadModlistForInstalling.Send(downloadedFile, null);
            NavigateToGlobal.Send(ScreenType.Installer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Protocol] Failed to download from Nexus Collection: {payload}", payload);
            Error = ValidationResult.Fail($"Failed to download Nexus Collection: {ex.Message}");
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;
            throw;
        }
    }

    private async Task SaveSettings()
    {
        if (_savingSettings) return;

        _savingSettings = true;
        await _settingsManager.Save("modlist_gallery", new GalleryFilterSettings
        {
            GameType = GameType,
            IncludeNSFW = IncludeNSFW,
            IncludeUnofficial = IncludeUnofficial,
            OnlyInstalled = OnlyInstalled,
            ExcludeMods = ExcludeMods,
        });
        _savingSettings = false;
    }

    private async Task LoadSettings()
    {
        using var ll = LoadingLock.WithLoading();
        RxApp.MainThreadScheduler.Schedule(await _settingsManager.Load<GalleryFilterSettings>("modlist_gallery"),
            (_, s) =>
            {
                SelectedGameTypeEntry = GameTypeEntries?.FirstOrDefault(gte => gte.GameIdentifier.Equals(s.GameType));
                IncludeNSFW = s.IncludeNSFW;
                IncludeUnofficial = s.IncludeUnofficial;
                OnlyInstalled = s.OnlyInstalled;
                ExcludeMods = s.ExcludeMods;
                return Disposable.Empty;
            });
    }

    private async Task LoadModLists()
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            var allowedTags = await _wjClient.LoadAllowedTags();
            var tagMappings = await _wjClient.LoadTagMappings();

            AllTags = allowedTags.Select(t => new ModListTag(t))
                                 .OrderBy(t => t.Name)
                                 .Prepend(new ModListTag("NSFW"))
                                 .Prepend(new ModListTag("Featured"))
                                 .Prepend(new ModListTag("Unavailable"))
                                 .ToHashSet();
            var searchIndex = await _wjClient.LoadSearchIndex();
            ModsPerList = searchIndex.ModsPerList;
            AllMods = searchIndex.AllMods.Select(mod => new ModListMod(mod)).ToHashSet();
            var modLists = await _wjClient.LoadLists();
            var modlistSummaries = (await _wjClient.GetListStatuses()).ToDictionary(summary => summary.MachineURL);
            foreach (var modlist in modLists)
            {
                var modlistTags = new List<string>();
                foreach(var tag in modlist.Tags)
                {
                    string? allowedTag = null;
                    tagMappings.TryGetValue(tag, out allowedTag);

                    if (allowedTags.TryGetValue(tag, out allowedTag))
                        modlistTags.Add(allowedTag);
                }
                if (modlist.NSFW) modlistTags.Insert(0, "NSFW");
                if (modlist.Official) modlistTags.Insert(0, "Featured");
                if ((modlist.ValidationSummary?.HasFailures ?? false) || modlist.ForceDown) modlistTags.Insert(0, "Unavailable");

                modlist.Tags = modlistTags;
            }

            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();
            var cacheManager = _serviceProvider.GetRequiredService<ImageCacheManager>();
            _modLists.Edit(e =>
            {
                e.Clear();
                e.AddOrUpdate(modLists.Select(m =>
                    new GalleryModListMetadataVM(_logger, this, m, _maintainer, modlistSummaries.TryGetValue(m.Links.MachineURL, out var summary) ? summary : null, _wjClient, _cancellationToken,
                        httpClient, cacheManager)));
            });
            DetermineListSizeRange();
            ll.Succeed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading lists");
            ll.Fail();
            throw;
        }
    }

    private void DetermineListSizeRange()
    {
        SmallestSizedModlist = null;
        LargestSizedModlist = null;
        foreach(var item in _modLists.Items)
        {
            if (SmallestSizedModlist == null) SmallestSizedModlist = item;
            if (LargestSizedModlist == null) LargestSizedModlist = item;

            var itemTotalSize = item.Metadata.DownloadMetadata.TotalSize;
            var smallestSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
            var largestSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;

            if (itemTotalSize < smallestSize) SmallestSizedModlist = item;

            if (itemTotalSize > largestSize) LargestSizedModlist = item;
        }
        MinModlistSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
        MaxModlistSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;
    }

    private void LoadGameTypeEntries()
    {
        GameTypeEntries = new(ModLists.Select(m => m.Metadata)
            .GroupBy(m => m.Game)
            .Select(g => new GameTypeEntry(g.Key.MetaData(), g.Count()))
            .OrderBy(gte => gte.GameMetaData.HumanFriendlyGameName)
            .Prepend(GameTypeEntry.GetAllGamesEntry(ModLists.Count))
            .ToList());
    }

    private Task EnsureGalleryLoadedAsync()
    {
        if (_galleryLoadedTcs.Task.IsCompleted)
            return _galleryLoadedTcs.Task;

        _loadModListsTask ??= LoadModListsSingleFlightAsync();

        return _galleryLoadedTcs.Task;
    }

    private async Task LoadModListsSingleFlightAsync()
    {
        await _loadModListsGate.WaitAsync(_cancellationToken);
        try
        {
            if (_galleryLoadedTcs.Task.IsCompleted)
                return;

            await LoadModLists();

            _galleryLoadedTcs.TrySetResult(true);
        }
        catch (OperationCanceledException oce)
        {
            _galleryLoadedTcs.TrySetCanceled(oce.CancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _galleryLoadedTcs.TrySetException(ex);
            throw;
        }
        finally
        {
            _loadModListsGate.Release();
        }
    }
}