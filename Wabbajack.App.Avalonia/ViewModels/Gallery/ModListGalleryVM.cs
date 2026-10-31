// Ported from the WPF app's ModListGalleryVM. What differs: bitmaps are Avalonia's, the two error popups are
// the same Windows message box called directly, the local file comes from the storage provider, and
// navigation goes through Navigator. The filtering, sorting, protocol handling and settings are unchanged.
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;

using DynamicData;
using DynamicData.Binding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ReactiveUI;
using ReactiveUI.SourceGenerators;

using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>The gallery filter choices that are remembered between runs, as the WPF app saved them.</summary>
public class GalleryFilterSettings
{
    public string GameType { get; set; }
    public bool IncludeNSFW { get; set; }
    public bool IncludeUnofficial { get; set; }
    public bool OnlyInstalled { get; set; }
    public string Search { get; set; }
    public bool ExcludeMods { get; set; }
}

public partial class ModListGalleryVM : ViewModel, ICanLoadLocalFileVM
{
    public partial class GameTypeEntry : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<Bitmap> _gameIcon;

        public GameTypeEntry(GameMetaData gameMetaData, int amount, IObservable<Bitmap> gameIcon)
        {
            GameMetaData = gameMetaData;
            IsAllGamesEntry = gameMetaData == null;
            GameIdentifier = IsAllGamesEntry ? ALL_GAME_IDENTIFIER : gameMetaData?.HumanFriendlyGameName;
            Amount = amount;
            _gameIcon = gameIcon.ToProperty(this, nameof(GameIcon), scheduler: RxApp.MainThreadScheduler);

            this.WhenAnyValue(x => x.Amount)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(FormattedName)));
        }

        public bool IsAllGamesEntry { get; set; }
        public GameMetaData GameMetaData { get; private set; }
        public Bitmap GameIcon => _gameIcon.Value;
        [Reactive] public partial int Amount { get; set; }
        public string FormattedName => IsAllGamesEntry ? $"{ALL_GAME_IDENTIFIER} ({Amount})" : $"{GameMetaData.HumanFriendlyGameName} ({Amount})";
        public string GameIdentifier { get; private set; }
        public static GameTypeEntry GetAllGamesEntry(int amount) => new(null, amount, Observable.Empty<Bitmap>());
    }

    /// <summary>True while the gallery is on screen; the tiles poll for downloaded lists only then.</summary>
    [Reactive] public partial bool IsActive { get; set; }

    private bool _savingSettings = false;
    private readonly SourceCache<GalleryModListMetadataVM, string> _modLists = new(x => x.Metadata.NamespacedName);
    public ReadOnlyObservableCollection<GalleryModListMetadataVM> _filteredModLists;

    public ReadOnlyObservableCollection<GalleryModListMetadataVM> ModLists => _filteredModLists;

    private const string ALL_GAME_IDENTIFIER = GalleryFilter.AllGames;

    [Reactive] public partial IValidationResult Error { get; set; }

    [Reactive] public partial string Search { get; set; }

    [Reactive] public partial bool OnlyInstalled { get; set; }

    [Reactive] public partial bool IncludeNSFW { get; set; }

    [Reactive] public partial bool IncludeUnofficial { get; set; }

    [Reactive] public partial bool ExcludeMods { get; set; }

    [Reactive] public partial string GameType { get; set; } = "All games";
    [Reactive] public partial double MinModlistSize { get; set; }
    [Reactive] public partial double MaxModlistSize { get; set; }

    public Dictionary<string, string> CommonlyWrongFormattedTags { get; set; } = new();
    [Reactive] public partial HashSet<ModListTag> AllTags { get; set; } = new();
    [Reactive] public partial ObservableCollection<ModListTag> HasTags { get; set; } = new();


    [Reactive] public partial HashSet<ModListMod> AllMods { get; set; } = new();
    [Reactive] public partial ObservableCollection<ModListMod> HasMods { get; set; } = new();
    [Reactive] public partial Dictionary<string, HashSet<string>> ModsPerList { get; set; } = new();

    [Reactive] public partial GalleryModListMetadataVM SmallestSizedModlist { get; set; }
    [Reactive] public partial GalleryModListMetadataVM LargestSizedModlist { get; set; }

    [Reactive] public partial ObservableCollection<GameTypeEntry> AllGameTypeEntries { get; set; } = new();
    [Reactive] public partial ObservableCollection<GameTypeEntry> GameTypeEntries { get; set; } = new();
    private GameTypeEntry _selectedGameTypeEntry = null;
    private bool _updatingGamesToFilter = false;

    public GameTypeEntry SelectedGameTypeEntry
    {
        get => _selectedGameTypeEntry;
        set
        {
            if (_selectedGameTypeEntry == value) return;
            var newEntry = value ?? GameTypeEntries?.FirstOrDefault(gte => gte.IsAllGamesEntry);
            RaiseAndSetIfChanged(ref _selectedGameTypeEntry, newEntry);
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
    private readonly GameIconCache _gameIcons;

    private readonly SemaphoreSlim _loadModListsGate = new(1, 1);
    private Task? _loadModListsTask;
    private readonly TaskCompletionSource<bool> _galleryLoadedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Reactive] public partial bool IsResolvingProtocol { get; set; }
    [Reactive] public partial string ProtocolStatusText { get; set; }

    private string? _protocolInFlightNamespacedName;

    public ICommand ResetFiltersCommand { get; set; }
    public ICommand LoadLocalFileCommand { get; set; }

    private readonly Navigator _navigator;

    public ModListGalleryVM(ILogger<ModListGalleryVM> logger, Client wjClient, GameLocator locator,
        SettingsManager settingsManager, ModListDownloadMaintainer maintainer, CancellationToken cancellationToken, IServiceProvider serviceProvider,
        GameIconCache gameIcons, FilePicker picker, Navigator navigator)
    {
        var searchThrottle = TimeSpan.FromSeconds(0.35);
        _wjClient = wjClient;
        _logger = logger;
        _locator = locator;
        _maintainer = maintainer;
        _settingsManager = settingsManager;
        _cancellationToken = cancellationToken;
        _serviceProvider = serviceProvider;
        _gameIcons = gameIcons;
        _navigator = navigator;

        ResetFiltersCommand = ReactiveCommand.Create(() =>
        {
            OnlyInstalled = false;
            IncludeNSFW = false;
            IncludeUnofficial = false;
            ExcludeMods = false;
            Search = string.Empty;
            SelectedGameTypeEntry = GameTypeEntries?.FirstOrDefault();
            HasTags = new ObservableCollection<ModListTag>();
            HasMods = new ObservableCollection<ModListMod>();
        });

        LoadLocalFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await picker.PickFile("Select a modlist", ("Wabbajack Modlist", "*" + Ext.Wabbajack));
            if (path != default && path.FileExists())
            {
                LoadModlistForInstalling.Send(path, null);
                _navigator.NavigateTo(ScreenType.Installer);
            }
        });

        this.WhenActivated(disposables =>
        {
            IsActive = true;
            Disposable.Create(() => IsActive = false).DisposeWith(disposables);

            EnsureGalleryLoadedAsync().FireAndForget();

            if (!IsSettingsLoaded)
                LoadSettings().FireAndForget();

            if (LoadModlistFromProtocol.TryConsumePending(out var pending))
            {
                _logger.LogInformation("[Protocol] Consumed pending machine URL on activation: {machineUrl}", pending);
                HandleProtocolLoad(pending).FireAndForget();
            }

            // A link that arrives while the gallery is already showing: navigating to it activates nothing, so
            // without this the link waited for the next time the gallery was opened. Taken through the same
            // pending slot, so a link that also activated the gallery is handled once.
            MessageBus.Current.Listen<LoadModlistFromProtocol>()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if (!LoadModlistFromProtocol.TryConsumePending(out var link)) return;
                    _logger.LogInformation("[Protocol] Received machine URL while the gallery was open: {machineUrl}", link);
                    HandleProtocolLoad(link).FireAndForget();
                })
                .DisposeWith(disposables);

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
                .StartWith(Search ?? "")
                .Select<string, Func<GalleryModListMetadataVM, bool>>(txt =>
                    item => GalleryFilter.MatchesSearch(item.Metadata, txt));

            var onlyInstalledGamesFilter = this.ObservableForProperty(vm => vm.OnlyInstalled)
                .Select(v => v.Value)
                .StartWith(OnlyInstalled)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(onlyInstalled =>
                    item => GalleryFilter.MatchesInstalled(item.Metadata, onlyInstalled, _locator.IsInstalled));

            var includeUnofficialFilter = this.ObservableForProperty(vm => vm.IncludeUnofficial)
                .Select(v => v.Value)
                .StartWith(IncludeUnofficial)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(unofficial =>
                    item => GalleryFilter.MatchesOfficial(item.Metadata, unofficial));

            var includeNSFWFilter = this.ObservableForProperty(vm => vm.IncludeNSFW)
                .Select(v => v.Value)
                .StartWith(IncludeNSFW)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(showNsfw =>
                    item => GalleryFilter.MatchesNsfw(item.Metadata, showNsfw));

            var gameFilter = this.ObservableForProperty(vm => vm.GameType)
                .Select(v => v.Value)
                .StartWith(GameType)
                .Select<string, Func<GalleryModListMetadataVM, bool>>(selected =>
                    item => GalleryFilter.MatchesGame(item.Metadata, selected));

            var minModlistSizeFilter = this.ObservableForProperty(vm => vm.MinModlistSize)
                .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                .Select(v => v.Value)
                .StartWith(MinModlistSize)
                .Select<double, Func<GalleryModListMetadataVM, bool>>(minModlistSize =>
                    item => GalleryFilter.MatchesSize(item.Metadata, minModlistSize, double.MaxValue));

            var maxModlistSizeFilter = this.ObservableForProperty(vm => vm.MaxModlistSize)
                .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                .Select(v => v.Value)
                .StartWith(MaxModlistSize)
                .Select<double, Func<GalleryModListMetadataVM, bool>>(maxModlistSize =>
                    item => GalleryFilter.MatchesSize(item.Metadata, double.MinValue, maxModlistSize));

            var includedTagsFilter = this.ObservableForProperty(vm => vm.HasTags)
                .Select(v => v.Value)
                .StartWith(HasTags)
                .Select<ObservableCollection<ModListTag>, Func<GalleryModListMetadataVM, bool>>(filteredTags =>
                {
                    var names = TagNames(filteredTags);
                    return item => GalleryFilter.MatchesTags(item.Metadata, names);
                });

            var includedModsFilter =
                this.WhenAnyValue(vm => vm.HasMods, vm => vm.ExcludeMods)
                    .Select<(ObservableCollection<ModListMod>, bool), Func<GalleryModListMetadataVM, bool>>(tuple =>
                    {
                        var (mods, exclude) = tuple;
                        var names = ModNames(mods);
                        // ModsPerList is read when an item is checked, not when the filter is built: the mod
                        // index can arrive after the selection.
                        return item => GalleryFilter.MatchesMods(item.Metadata, ModsPerList, names, exclude);
                    });


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
                    UpdateGamesToFilter();
                })
                .DisposeWith(disposables);

            // ModLists is only assigned above, on activation; say so, or a binding made before it keeps null.
            this.RaisePropertyChanged(nameof(ModLists));
        });
    }

    private static IReadOnlyCollection<string> TagNames(IEnumerable<ModListTag>? tags) =>
        tags?.Select(t => t.Name).ToArray() ?? Array.Empty<string>();

    private static IReadOnlyCollection<string> ModNames(IEnumerable<ModListMod>? mods) =>
        mods?.Select(m => m.Name).ToArray() ?? Array.Empty<string>();

    private void UpdateGamesToFilter()
    {
        if (AllGameTypeEntries == null || !AllGameTypeEntries.Any() || _updatingGamesToFilter) return;

        try
        {
            _updatingGamesToFilter = true;
            // Every filter but the game's, through the same rules the list uses, so a game's count is what
            // choosing it then shows.
            var criteria = new GalleryCriteria(Search?.Trim() ?? "", OnlyInstalled, IncludeUnofficial, IncludeNSFW,
                MinModlistSize, MaxModlistSize, TagNames(HasTags), ModNames(HasMods), ExcludeMods);
            var modsPerList = ModsPerList;

            var filteredItems = _modLists.Items
                .Where(item => GalleryFilter.MatchesAllButGame(item.Metadata, criteria, _locator.IsInstalled, modsPerList))
                .ToList();

            var counts = filteredItems
                .GroupBy(m => m.Metadata.Game)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalCount = filteredItems.Count;

            var selectedIdentifier = SelectedGameTypeEntry?.GameIdentifier;

            foreach (var entry in AllGameTypeEntries)
            {
                if (entry.IsAllGamesEntry)
                {
                    entry.Amount = totalCount;
                }
                else
                {
                    var game = entry.GameMetaData.Game;
                    entry.Amount = counts.TryGetValue(game, out var count) ? count : 0;
                }
            }

            // Synchronize GameTypeEntries to only show those with Amount > 0
            var toShow = AllGameTypeEntries.Where(e => e.IsAllGamesEntry || e.Amount > 0).ToList();

            // Update GameTypeEntries collection while preserving selection
            var currentEntries = GameTypeEntries.ToList();
            if (!currentEntries.SequenceEqual(toShow))
            {
                GameTypeEntries.Clear();
                foreach (var entry in toShow)
                    GameTypeEntries.Add(entry);

                // Re-select if possible
                if (selectedIdentifier == null) return;
                var match = GameTypeEntries.FirstOrDefault(e => e.GameIdentifier == selectedIdentifier);
                var fallback = GameTypeEntries.FirstOrDefault(e => e.IsAllGamesEntry);
                var toSelect = match ?? fallback;
                if (!ReferenceEquals(SelectedGameTypeEntry, toSelect))
                    SelectedGameTypeEntry = toSelect;
            }
        }
        finally
        {
            _updatingGamesToFilter = false;
        }
    }

    public void Unload()
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
            var link = ProtocolLink.Parse(payload);
            switch (link)
            {
                case ProtocolLink.NexusCollection collection:
                    await HandleNexusCollection(collection.Slug, collection.Revision);
                    ll.Succeed();
                    return;
                case ProtocolLink.DirectDownload download:
                    await HandleDirectDownload(download.Url, download.FileName);
                    ll.Succeed();
                    return;
                case ProtocolLink.Invalid invalid:
                    _logger.LogWarning("[Protocol] {reason}", invalid.Reason);
                    Error = ValidationResult.Fail(invalid.Reason);
                    IsResolvingProtocol = false;
                    ProtocolStatusText = string.Empty;
                    ll.Fail();
                    return;
            }

            var gallery = (ProtocolLink.GalleryList) link;
            ProtocolStatusText = $"Preparing to install {gallery.Id}…";

            await EnsureGalleryLoadedAsync();

            // repo/list, or the short list name alone.
            var modlist = _modLists.Items.FirstOrDefault(m => (gallery.Namespaced
                    ? m.Metadata.NamespacedName
                    : m.Metadata.Links.MachineURL)
                .Equals(gallery.Id, StringComparison.OrdinalIgnoreCase));

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

            // Whether the game is installed is preflight's question, not this one's: a game that is not here can
            // still be installed for, with its files fetched from Steam, and preflight is what offers that.
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

    private async Task HandleDirectDownload(Uri url, string fileName)
    {
        _logger.LogInformation("[Protocol] Handling direct download from: {url}", url);

        ProtocolStatusText = "Downloading modlist file…";

        try
        {
            var downloadDir = KnownFolders.EntryPoint.Combine("downloaded_mod_lists");
            if (!downloadDir.DirectoryExists())
                downloadDir.CreateDirectory();

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

            // Nothing else clears the overlay for a direct download: the listener above only recognises a
            // gallery list it was told to wait for.
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;

            _logger.LogInformation("[Protocol] Loading modlist from downloaded file");
            LoadModlistForInstalling.Send(downloadedFile, null);
            _navigator.NavigateTo(ScreenType.Installer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Protocol] Failed to download modlist from: {url}", url);
            Error = ValidationResult.Fail($"Failed to download modlist: {ex.Message}");
            IsResolvingProtocol = false;
            ProtocolStatusText = string.Empty;
            throw;
        }
    }

    private async Task HandleNexusCollection(string slug, int? revisionNumber)
    {
        _logger.LogInformation("[Protocol] Handling Nexus Collection: {slug}", slug);

        ProtocolStatusText = "Fetching collection from Nexus Mods…";

        try
        {
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
                NativeMessageBox.ShowError(errorMessage, "Nexus Collection Download Failed");

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
            _navigator.NavigateTo(ScreenType.Installer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Protocol] Failed to download from Nexus Collection: {slug}", slug);
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

    public bool IsSettingsLoaded { get; private set; }

    private string _pendingGameTypeFromSettings = null;

    private async Task LoadSettings()
    {
        using var ll = LoadingLock.WithLoading();
        RxApp.MainThreadScheduler.Schedule(await _settingsManager.Load<GalleryFilterSettings>("modlist_gallery"),
            (_, s) =>
            {
                if (AllGameTypeEntries == null || !AllGameTypeEntries.Any())
                {
                    _pendingGameTypeFromSettings = s.GameType;
                }
                else
                {
                    SelectedGameTypeEntry = GameTypeEntries?.FirstOrDefault(gte => gte.GameIdentifier == s.GameType);
                }
                IncludeNSFW = s.IncludeNSFW;
                IncludeUnofficial = s.IncludeUnofficial;
                OnlyInstalled = s.OnlyInstalled;
                ExcludeMods = s.ExcludeMods;
                IsSettingsLoaded = true;
                return Disposable.Empty;
            });
    }

    private async Task LoadModLists()
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            var snapshot = await Task.Run(LoadGallerySnapshot, _cancellationToken);

            AllTags = snapshot.AllTags;
            ModsPerList = snapshot.ModsPerList;
            AllMods = snapshot.AllMods;
            _modLists.Edit(e =>
            {
                e.Clear();
                e.AddOrUpdate(snapshot.ModLists);
            });
            LoadGameTypeEntries();
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

    private sealed record GallerySnapshot(
        HashSet<ModListTag> AllTags,
        HashSet<ModListMod> AllMods,
        Dictionary<string, HashSet<string>> ModsPerList,
        List<GalleryModListMetadataVM> ModLists);

    private async Task<GallerySnapshot> LoadGallerySnapshot()
    {
        var allowedTags = await _wjClient.LoadAllowedTags();
        var tagMappings = await _wjClient.LoadTagMappings();

        var allTags = allowedTags.Select(t => new ModListTag(t))
            .OrderBy(t => t.Name)
            .Prepend(new ModListTag("NSFW"))
            .Prepend(new ModListTag("Featured"))
            .Prepend(new ModListTag("Unavailable"))
            .ToHashSet();
        var searchIndex = await _wjClient.LoadSearchIndex();
        var allMods = searchIndex.AllMods.Select(mod => new ModListMod(mod)).ToHashSet();
        var modLists = await _wjClient.LoadLists();
        var modlistSummaries = (await _wjClient.GetListStatuses()).ToDictionary(summary => summary.MachineURL);
        foreach (var modlist in modLists)
        {
            modlist.Tags = GalleryTags.Normalize(modlist.Tags, allowedTags, tagMappings, modlist.NSFW,
                modlist.Official, (modlist.ValidationSummary?.HasFailures ?? false) || modlist.ForceDown);
        }

        var httpClient = _serviceProvider.GetRequiredService<HttpClient>();
        var cacheManager = _serviceProvider.GetRequiredService<ImageCacheManager>();
        var modListViewModels = modLists.Select(m =>
                new GalleryModListMetadataVM(_logger, this, m, _maintainer, modlistSummaries.TryGetValue(m.Links.MachineURL, out var summary) ? summary : null, _wjClient, _cancellationToken,
                    httpClient, cacheManager, _gameIcons, _navigator))
            .ToList();

        return new GallerySnapshot(allTags, allMods, searchIndex.ModsPerList, modListViewModels);
    }

    private void DetermineListSizeRange()
    {
        SmallestSizedModlist = null;
        LargestSizedModlist = null;
        foreach (var item in _modLists.Items)
        {
            if (SmallestSizedModlist == null) SmallestSizedModlist = item;
            if (LargestSizedModlist == null) LargestSizedModlist = item;

            var itemTotalSize = item.Metadata.DownloadMetadata.TotalSize;
            var smallestSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
            var largestSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;

            if (itemTotalSize < smallestSize) SmallestSizedModlist = item;

            if (itemTotalSize > largestSize) LargestSizedModlist = item;
        }

        // An empty gallery (every repository down, or a failed load) has no sizes to bound the slider with.
        if (SmallestSizedModlist == null || LargestSizedModlist == null) return;
        MinModlistSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
        MaxModlistSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;
    }

    private void LoadGameTypeEntries()
    {
        var entries = _modLists.Items.Select(m => m.Metadata)
            .GroupBy(m => m.Game)
            .Select(g =>
            {
                var gameMetaData = g.Key.MetaData();
                return new GameTypeEntry(gameMetaData, g.Count(), _gameIcons.Get(gameMetaData.IconSource));
            })
            .OrderBy(gte => gte.GameMetaData.HumanFriendlyGameName)
            .Prepend(GameTypeEntry.GetAllGamesEntry(_modLists.Count))
            .ToList();

        AllGameTypeEntries.Clear();
        AllGameTypeEntries.AddRange(entries);

        UpdateGamesToFilter();

        if (_pendingGameTypeFromSettings != null)
        {
            SelectedGameTypeEntry = GameTypeEntries.FirstOrDefault(gte => gte.GameIdentifier == _pendingGameTypeFromSettings)
                                    ?? GameTypeEntries.FirstOrDefault(gte => gte.IsAllGamesEntry);
            _pendingGameTypeFromSettings = null;
        }
        else if (SelectedGameTypeEntry == null)
        {
            SelectedGameTypeEntry = GameTypeEntries.FirstOrDefault(gte => gte.IsAllGamesEntry);
        }
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
