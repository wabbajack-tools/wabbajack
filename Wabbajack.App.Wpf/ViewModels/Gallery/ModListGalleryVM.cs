using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack;

public class ModListGalleryVM : BackNavigatingVM
{
    public MainWindowVM MWVM { get; }

    private readonly SourceCache<GalleryModListMetadataVM, string> _modLists = new(x => x.Metadata.NamespacedName);
    public ReadOnlyObservableCollection<GalleryModListMetadataVM> _filteredModLists;

    public ReadOnlyObservableCollection<GalleryModListMetadataVM> ModLists => _filteredModLists;

    private const string ALL_GAME_IDENTIFIER = "All games";

    [Reactive] public IErrorResponse Error { get; set; }

    [Reactive] public string Search { get; set; }

    [Reactive] public bool OnlyInstalled { get; set; }

    [Reactive] public bool ShowNSFW { get; set; }

    [Reactive] public bool ShowUnofficialLists { get; set; }

    [Reactive] public string GameType { get; set; }
    [Reactive] public double MinModlistSize { get; set; }
    [Reactive] public double MaxModlistSize { get; set; }

    [Reactive] public GalleryModListMetadataVM SmallestSizedModlist { get; set; }
    [Reactive] public GalleryModListMetadataVM LargestSizedModlist { get; set; }

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

    [Reactive] public List<GameTypeEntry> GameTypeEntries { get; set; }
    private bool _filteringOnGame;
    private GameTypeEntry _selectedGameTypeEntry = null;

    public GameTypeEntry SelectedGameTypeEntry
    {
        get => _selectedGameTypeEntry;
        set
        {
            RaiseAndSetIfChanged(ref _selectedGameTypeEntry, value == null ? GameTypeEntries?.FirstOrDefault(gte => gte.IsAllGamesEntry) : value);
            GameType = _selectedGameTypeEntry?.GameIdentifier;
        }
    }

    private readonly Client _wjClient;
    private readonly ILogger<ModListGalleryVM> _logger;
    private readonly GameLocator _locator;
    private readonly ModListDownloadMaintainer _maintainer;
    private readonly SettingsManager _settingsManager;
    private readonly CancellationToken _cancellationToken;

    public ICommand ClearFiltersCommand { get; set; }

    public ModListGalleryVM(ILogger<ModListGalleryVM> logger, Client wjClient, GameLocator locator,
        SettingsManager settingsManager, ModListDownloadMaintainer maintainer, CancellationToken cancellationToken)
        : base(logger)
    {
        var searchThrottle = TimeSpan.FromSeconds(0.5);
        _wjClient = wjClient;
        _logger = logger;
        _locator = locator;
        _maintainer = maintainer;
        _settingsManager = settingsManager;
        _cancellationToken = cancellationToken;

        ClearFiltersCommand = ReactiveCommand.Create(
            () =>
            {
                OnlyInstalled = false;
                ShowNSFW = false;
                ShowUnofficialLists = false;
                Search = string.Empty;
                SelectedGameTypeEntry = GameTypeEntries.FirstOrDefault();
            });

        BackCommand = ReactiveCommand.Create(
            () =>
            {
                NavigateToGlobal.Send(ScreenType.Home);
            });


        this.WhenActivated(disposables =>
        {
            LoadModLists().FireAndForget();
            LoadSettings().FireAndForget();

            Disposable.Create(() => SaveSettings().FireAndForget())
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

            var showUnofficial = this.ObservableForProperty(vm => vm.ShowUnofficialLists)
                .Select(v => v.Value)
                .StartWith(false)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(unoffical =>
                {
                    if (unoffical) return x => true;
                    return x => x.Metadata.Official;
                });

            var showNSFWFilter = this.ObservableForProperty(vm => vm.ShowNSFW)
                .Select(v => v.Value)
                .Select<bool, Func<GalleryModListMetadataVM, bool>>(showNsfw => { return item => item.Metadata.NSFW == showNsfw; })
                .StartWith(item => item.Metadata.NSFW == false);

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
                                

            var searchSorter = this.WhenValueChanged(vm => vm.Search)
                                    .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                                    .Select(s => SortExpressionComparer<GalleryModListMetadataVM>
                                                 .Descending(m => m.Metadata.Title.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase))
                                                 .ThenByDescending(m => m.Metadata.Title.Contains(s ?? "", StringComparison.InvariantCultureIgnoreCase))
                                                 .ThenByDescending(m => !m.IsBroken));
            _modLists.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Filter(searchTextPredicates)
                .Filter(onlyInstalledGamesFilter)
                .Filter(showUnofficial)
                .Filter(showNSFWFilter)
                .Filter(gameFilter)
                .Filter(minModlistSizeFilter)
                .Filter(maxModlistSizeFilter)
                .Sort(searchSorter)
                .TreatMovesAsRemoveAdd()
                .Bind(out _filteredModLists)
                .Subscribe((_) =>
                {
                    if (!_filteringOnGame)
                    {
                        var previousGameType = GameType;
                        SelectedGameTypeEntry = null;
                        GameTypeEntries = new(GetGameTypeEntries());
                        var nextEntry = GameTypeEntries.FirstOrDefault(gte => previousGameType == gte.GameIdentifier);
                        SelectedGameTypeEntry = nextEntry != default ? nextEntry : GameTypeEntries.FirstOrDefault(gte => GameType == ALL_GAME_IDENTIFIER);
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

    private async Task SaveSettings()
    {
        await _settingsManager.Save("modlist_gallery", new GalleryFilterSettings
        {
            GameType = GameType,
            ShowNSFW = ShowNSFW,
            ShowUnofficialLists = ShowUnofficialLists,
            Search = Search,
            OnlyInstalled = OnlyInstalled,
        });
    }

    private async Task LoadSettings()
    {
        using var ll = LoadingLock.WithLoading();
        RxApp.MainThreadScheduler.Schedule(await _settingsManager.Load<GalleryFilterSettings>("modlist_gallery"),
            (_, s) =>
        {
            SelectedGameTypeEntry = GameTypeEntries?.FirstOrDefault(gte => gte.GameIdentifier.Equals(s.GameType));
            ShowNSFW = s.ShowNSFW;
            ShowUnofficialLists = s.ShowUnofficialLists;
            Search = s.Search;
            OnlyInstalled = s.OnlyInstalled;
            return Disposable.Empty;
        });
    }

    private async Task LoadModLists()
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            var modLists = await _wjClient.LoadLists();
            _modLists.Edit(e =>
            {
                e.Clear();
                e.AddOrUpdate(modLists.Select(m =>
                    new GalleryModListMetadataVM(_logger, this, m, _maintainer, _wjClient, _cancellationToken)));
            });
            SmallestSizedModlist = _modLists.Items.Any() ? _modLists.Items.MinBy(ml => ml.Metadata.DownloadMetadata.TotalSize) : null;
            LargestSizedModlist = _modLists.Items.Any() ? _modLists.Items.MaxBy(ml => ml.Metadata.DownloadMetadata.TotalSize) : null;
            MinModlistSize = SmallestSizedModlist.Metadata.DownloadMetadata.TotalSize;
            MaxModlistSize = LargestSizedModlist.Metadata.DownloadMetadata.TotalSize;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading lists");
            ll.Fail();
        }
        ll.Succeed();
    }

    private List<GameTypeEntry> GetGameTypeEntries()
    {
        return ModLists.Select(fm => fm.Metadata)
               .GroupBy(m => m.Game)
               .Select(g => new GameTypeEntry(g.Key.MetaData(), g.Count()))
               .OrderBy(gte => gte.GameMetaData.HumanFriendlyGameName)
               .Prepend(GameTypeEntry.GetAllGamesEntry(ModLists.Count))
               .ToList();
    }
}