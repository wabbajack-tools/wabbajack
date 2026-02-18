using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

public class ModListGalleryVM : ViewModelBase
{
    public class GameTypeEntry
    {
        public GameTypeEntry(GameMetaData? gameMetaData, int amount)
        {
            GameMetaData = gameMetaData;
            IsAllGamesEntry = gameMetaData == null;
            GameIdentifier = IsAllGamesEntry ? ALL_GAME_IDENTIFIER : gameMetaData!.HumanFriendlyGameName;
            Amount = amount;
            FormattedName = IsAllGamesEntry
                ? $"{ALL_GAME_IDENTIFIER} ({Amount})"
                : $"{gameMetaData!.HumanFriendlyGameName} ({Amount})";
        }

        public bool IsAllGamesEntry { get; }
        public GameMetaData? GameMetaData { get; }
        public int Amount { get; }
        public string FormattedName { get; }
        public string GameIdentifier { get; }

        public static GameTypeEntry GetAllGamesEntry(int amount) => new(null, amount);
    }

    private bool _savingSettings;
    private readonly SourceCache<GalleryModListMetadataVM, string> _modLists = new(x => x.Metadata.NamespacedName);
    private ReadOnlyObservableCollection<GalleryModListMetadataVM> _filteredModLists = null!;

    public ReadOnlyObservableCollection<GalleryModListMetadataVM> ModLists => _filteredModLists;

    private const string ALL_GAME_IDENTIFIER = "All games";

    [Reactive] public string? Search { get; set; }
    [Reactive] public bool OnlyInstalled { get; set; }
    [Reactive] public bool IncludeNSFW { get; set; }
    [Reactive] public bool IncludeUnofficial { get; set; }
    [Reactive] public bool ExcludeMods { get; set; }
    [Reactive] public string? GameType { get; set; }
    [Reactive] public double MinModlistSize { get; set; }
    [Reactive] public double MaxModlistSize { get; set; }

    [Reactive] public HashSet<ModListTag> AllTags { get; set; } = new();
    [Reactive] public ObservableCollection<ModListTag> HasTags { get; set; } = new();
    [Reactive] public HashSet<ModListMod> AllMods { get; set; } = new();
    [Reactive] public ObservableCollection<ModListMod> HasMods { get; set; } = new();
    [Reactive] public Dictionary<string, HashSet<string>> ModsPerList { get; set; } = new();

    [Reactive] public GalleryModListMetadataVM? SmallestSizedModlist { get; set; }
    [Reactive] public GalleryModListMetadataVM? LargestSizedModlist { get; set; }

    [Reactive] public ObservableCollection<GameTypeEntry>? GameTypeEntries { get; set; }

    private bool _filteringOnGame;
    private GameTypeEntry? _selectedGameTypeEntry;

    public GameTypeEntry? SelectedGameTypeEntry
    {
        get => _selectedGameTypeEntry;
        set
        {
            RaiseAndSetIfChanged(ref _selectedGameTypeEntry,
                value ?? GameTypeEntries?.FirstOrDefault(gte => gte.IsAllGamesEntry));
            GameType = _selectedGameTypeEntry?.GameIdentifier;
        }
    }

    public ICommand ResetFiltersCommand { get; }

    private readonly Client _wjClient;
    private readonly ILogger<ModListGalleryVM> _logger;
    private readonly GameLocator _locator;
    private readonly ModListDownloadMaintainer _maintainer;
    private readonly SettingsManager _settingsManager;
    private readonly CancellationToken _cancellationToken;
    private readonly IServiceProvider _serviceProvider;

    public ModListGalleryVM(
        ILogger<ModListGalleryVM> logger,
        Client wjClient,
        GameLocator locator,
        SettingsManager settingsManager,
        ModListDownloadMaintainer maintainer,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider)
    {
        var searchThrottle = TimeSpan.FromSeconds(0.35);
        _wjClient = wjClient;
        _logger = logger;
        _locator = locator;
        _maintainer = maintainer;
        _settingsManager = settingsManager;
        _cancellationToken = cancellationToken;
        _serviceProvider = serviceProvider;

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

        this.WhenActivated(disposables =>
        {
            LoadModLists().FireAndForget();
            LoadSettings().FireAndForget();

            this.WhenAnyValue(x => x.IncludeNSFW, x => x.IncludeUnofficial, x => x.OnlyInstalled,
                              x => x.GameType, x => x.ExcludeMods)
                .Subscribe(_ => SaveSettings().FireAndForget())
                .DisposeWith(disposables);

            var searchTextPredicates = this.ObservableForProperty(vm => vm.Search)
                .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                .Select(c => c.Value?.Trim() ?? "")
                .StartWith(Search ?? "")
                .Select<string, Func<GalleryModListMetadataVM, bool>>(txt =>
                {
                    if (string.IsNullOrWhiteSpace(txt)) return _ => true;
                    return item => item.Metadata.Title.ContainsCaseInsensitive(txt)
                                || item.Metadata.Description.ContainsCaseInsensitive(txt)
                                || item.Metadata.Tags.Contains(txt);
                });

            var onlyInstalledGamesFilter = this.ObservableForProperty(vm => vm.OnlyInstalled)
                .Select(v => v.Value)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(onlyInstalled =>
                {
                    if (!onlyInstalled) return _ => true;
                    return item => _locator.IsInstalled(item.Metadata.Game);
                })
                .StartWith(_ => true);

            var includeUnofficialFilter = this.ObservableForProperty(vm => vm.IncludeUnofficial)
                .Select(v => v.Value)
                .StartWith(IncludeUnofficial)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(unofficial =>
                {
                    if (unofficial) return _ => true;
                    return x => x.Metadata.Official;
                });

            var includeNSFWFilter = this.ObservableForProperty(vm => vm.IncludeNSFW)
                .Select(v => v.Value)
                .StartWith(IncludeNSFW)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(showNsfw =>
                {
                    if (showNsfw) return _ => true;
                    return x => !x.Metadata.NSFW;
                });

            var gameFilter = this.ObservableForProperty(vm => vm.GameType)
                .Select(v => v.Value)
                .Select<string?, Func<GalleryModListMetadataVM, bool>>(selected =>
                {
                    _filteringOnGame = true;
                    if (selected is null or ALL_GAME_IDENTIFIER) return _ => true;
                    return item => item.Metadata.Game.MetaData().HumanFriendlyGameName == selected;
                })
                .StartWith(_ => true);

            var minSizeFilter = this.ObservableForProperty(vm => vm.MinModlistSize)
                .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                .Select(v => v.Value)
                .Select<double, Func<GalleryModListMetadataVM, bool>>(min =>
                    item => item.Metadata.DownloadMetadata.TotalSize >= min);

            var maxSizeFilter = this.ObservableForProperty(vm => vm.MaxModlistSize)
                .Throttle(TimeSpan.FromSeconds(0.05), RxApp.MainThreadScheduler)
                .Select(v => v.Value)
                .Select<double, Func<GalleryModListMetadataVM, bool>>(max =>
                    item => item.Metadata.DownloadMetadata.TotalSize <= max);

            var includedTagsFilter = this.ObservableForProperty(vm => vm.HasTags)
                .Select(v => v.Value)
                .Select<ObservableCollection<ModListTag>, Func<GalleryModListMetadataVM, bool>>(filteredTags =>
                {
                    if (filteredTags?.Any() != true) return _ => true;
                    return item => filteredTags.All(tag => item.Metadata.Tags.Contains(tag.Name));
                })
                .StartWith(_ => true);

            var includedModsFilter = this.ObservableForProperty(vm => vm.HasMods)
                .CombineLatest(this.ObservableForProperty(vm => vm.ExcludeMods).Select(v => v.Value))
                .Select(t => (Mods: t.First.Value, Exclude: t.Second))
                .Select<(ObservableCollection<ModListMod> Mods, bool Exclude), Func<GalleryModListMetadataVM, bool>>(fd =>
                {
                    if (fd.Mods?.Any() != true) return _ => true;
                    if (fd.Exclude)
                        return item =>
                            !ModsPerList.TryGetValue(item.Metadata.Links.MachineURL, out var mods) ||
                            !fd.Mods.Any(mod => mods.Contains(mod.Name));
                    return item =>
                        ModsPerList.TryGetValue(item.Metadata.Links.MachineURL, out var mods) &&
                        fd.Mods.All(mod => mods.Contains(mod.Name));
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
                .Filter(minSizeFilter)
                .Filter(maxSizeFilter)
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
                        var nextEntry = GameTypeEntries?.FirstOrDefault(gte => previousGameType == gte.GameIdentifier);
                        SelectedGameTypeEntry = nextEntry ?? GameTypeEntries?.FirstOrDefault(gte => GameType == ALL_GAME_IDENTIFIER);
                    }
                    _filteringOnGame = false;
                })
                .DisposeWith(disposables);
        });
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
        RxApp.MainThreadScheduler.Schedule(
            await _settingsManager.Load<GalleryFilterSettings>("modlist_gallery"),
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
            var summaries = (await _wjClient.GetListStatuses()).ToDictionary(s => s.MachineURL);

            foreach (var modlist in modLists)
            {
                var modlistTags = new List<string>();
                foreach (var tag in modlist.Tags)
                {
                    if (tagMappings.TryGetValue(tag, out var mapped))
                        modlistTags.Add(mapped);
                    else if (allowedTags.TryGetValue(tag, out var direct))
                        modlistTags.Add(direct);
                }
                if (modlist.NSFW) modlistTags.Insert(0, "NSFW");
                if (modlist.Official) modlistTags.Insert(0, "Featured");
                if ((modlist.ValidationSummary?.HasFailures ?? false) || modlist.ForceDown)
                    modlistTags.Insert(0, "Unavailable");
                modlist.Tags = modlistTags;
            }

            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();
            var cacheManager = _serviceProvider.GetRequiredService<ImageCacheManager>();

            _modLists.Edit(e =>
            {
                e.Clear();
                e.AddOrUpdate(modLists.Select(m =>
                    new GalleryModListMetadataVM(
                        _logger, this, m, _maintainer,
                        summaries.TryGetValue(m.Links.MachineURL, out var sum) ? sum : null,
                        _wjClient, _cancellationToken, httpClient, cacheManager)));
            });

            DetermineListSizeRange();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading lists");
            ll.Fail();
        }
        ll.Succeed();
    }

    private void DetermineListSizeRange()
    {
        SmallestSizedModlist = null;
        LargestSizedModlist = null;
        foreach (var item in _modLists.Items)
        {
            SmallestSizedModlist ??= item;
            LargestSizedModlist ??= item;
            var size = item.Metadata.DownloadMetadata.TotalSize;
            if (size < SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize) SmallestSizedModlist = item;
            if (size > LargestSizedModlist.Metadata.DownloadMetadata.TotalSize) LargestSizedModlist = item;
        }
        if (SmallestSizedModlist != null)
            MinModlistSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
        if (LargestSizedModlist != null)
            MaxModlistSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;
    }

    private void LoadGameTypeEntries()
    {
        GameTypeEntries = new ObservableCollection<GameTypeEntry>(
            ModLists.Select(m => m.Metadata)
                .GroupBy(m => m.Game)
                .Select(g => new GameTypeEntry(g.Key.MetaData(), g.Count()))
                .OrderBy(gte => gte.GameMetaData?.HumanFriendlyGameName)
                .Prepend(GameTypeEntry.GetAllGamesEntry(ModLists.Count)));
    }
}
