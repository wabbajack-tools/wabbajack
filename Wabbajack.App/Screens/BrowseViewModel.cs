using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Controls;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.App.Screens;

public class BrowseViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly Configuration _configuration;
    private readonly DownloadDispatcher _dispatcher;
    private readonly IResource<DownloadDispatcher> _dispatcherLimiter;
    private readonly DTOSerializer _dtos;
    public readonly ReadOnlyObservableCollection<GameSelectorItemViewModel> _filteredGamesList;

    public readonly ReadOnlyObservableCollection<BrowseItemViewModel> _filteredModLists;
    private readonly GameLocator _gameLocator;
    private readonly FileHashCache _hashCache;
    private readonly HttpClient _httpClient;
    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger<BrowseViewModel> _logger;
    private readonly Client _wjClient;

    private readonly SourceCache<GameSelectorItemViewModel, string> _gamesList = new(x => x.Name);

    private readonly SourceCache<BrowseItemViewModel, string> _modLists = new(x => x.MachineURL);

    public BrowseViewModel(ILogger<BrowseViewModel> logger, Client wjClient, HttpClient httpClient,
        IResource<HttpClient> limiter, FileHashCache hashCache,
        IResource<DownloadDispatcher> dispatcherLimiter, DownloadDispatcher dispatcher, GameLocator gameLocator,
        DTOSerializer dtos, Configuration configuration)
    {
        Activator = new ViewModelActivator();
        _wjClient = wjClient;
        _logger = logger;
        _httpClient = httpClient;
        _limiter = limiter;
        _hashCache = hashCache;
        _configuration = configuration;
        _dispatcher = dispatcher;
        _dispatcherLimiter = dispatcherLimiter;
        _gameLocator = gameLocator;
        _dtos = dtos;


        var searchTextPredicates = this.ObservableForProperty(vm => vm.SearchText)
            .Select(change => change.Value)
            .StartWith("")
            .Select<string, Func<BrowseItemViewModel, bool>>(txt =>
            {
                if (string.IsNullOrWhiteSpace(txt)) return _ => true;
                return item => item.Title.Contains(txt, StringComparison.InvariantCultureIgnoreCase) ||
                               item.Description.Contains(txt, StringComparison.InvariantCultureIgnoreCase);
            });


        _gamesList.Edit(e =>
        {
            e.Clear();
            foreach (var game in GameRegistry.Games.Keys) e.AddOrUpdate(new GameSelectorItemViewModel(game));
        });

        _gamesList.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Sort(Comparer<GameSelectorItemViewModel>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)))
            .Bind(out _filteredGamesList)
            .Subscribe();

        var gameFilter = this.ObservableForProperty(vm => vm.SelectedGame)
            .Select(v => v.Value)
            .Select<GameSelectorItemViewModel?, Func<BrowseItemViewModel, bool>>(selected =>
            {
                if (selected == null) return _ => true;
                return item => item.Game == selected.Game;
            })
            .StartWith(_ => true);

        var onlyInstalledGamesFilter = this.ObservableForProperty(vm => vm.OnlyInstalledGames)
            .Select(v => v.Value)
            .Select<bool, Func<BrowseItemViewModel, bool>>(onlyInstalled =>
            {
                if (onlyInstalled == false) return _ => true;
                return item => _gameLocator.IsInstalled(item.Game);
            })
            .StartWith(_ => true);

        var onlyUtilityListsFilter = this.ObservableForProperty(vm => vm.OnlyUtilityLists)
            .Select(v => v.Value)
            .Select<bool, Func<BrowseItemViewModel, bool>>(utility =>
            {
                if (utility == false) return item => item.IsUtilityList == false;
                return item => item.IsUtilityList;
            })
            .StartWith(item => item.IsUtilityList == false);

        var showNSFWFilter = this.ObservableForProperty(vm => vm.ShowNSFW)
            .Select(v => v.Value)
            .Select<bool, Func<BrowseItemViewModel, bool>>(showNSFW => { return item => item.IsNSFW == showNSFW; })
            .StartWith(item => item.IsNSFW == false);


        _modLists.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Filter(searchTextPredicates)
            .Filter(gameFilter)
            .Filter(onlyInstalledGamesFilter)
            .Filter(onlyUtilityListsFilter)
            .Filter(showNSFWFilter)
            .Bind(out _filteredModLists)
            .Subscribe();

        ResetFiltersCommand = ReactiveCommand.Create(() =>
        {
            SelectedGame = null;
            SearchText = "";
        });


        this.WhenActivated(disposables =>
        {
            LoadSettings().FireAndForget();
            LoadData().FireAndForget();

            Disposable.Create(() => { SaveSettings().FireAndForget(); }).DisposeWith(disposables);

            /*
            var searchTextFilter = this.ObservableForProperty(view => view.SearchText)
                .Select<IObservedChange<BrowseViewModel, string>, Func<>>(text =>
                {
                    if (string.IsNullOrWhiteSpace(text.Value))
                        return lst => true;
                    return 
                })*/
        });
    }

    public ReadOnlyObservableCollection<BrowseItemViewModel> ModLists => _filteredModLists;
    public ReadOnlyObservableCollection<GameSelectorItemViewModel> GamesList => _filteredGamesList;

    [Reactive] public GameSelectorItemViewModel? SelectedGame { get; set; }

    [Reactive] public string SearchText { get; set; }

    [Reactive] public bool OnlyInstalledGames { get; set; }

    [Reactive] public bool OnlyUtilityLists { get; set; }

    [Reactive] public bool ShowNSFW { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> ResetFiltersCommand { get; set; }

    private AbsolutePath SavedSettingsLocation => _configuration.SavedSettingsLocation.Combine("browse_view.json");

    private async Task LoadData()
    {
        var modlists = await _wjClient.LoadLists();
        var summaries = (await _wjClient.GetListStatuses()).ToDictionary(m => m.MachineURL);
        var vms = modlists.Select(m =>
        {
            if (!summaries.TryGetValue(m.Links.MachineURL, out var summary)) summary = new ModListSummary();

            return new BrowseItemViewModel(m, summary, _httpClient, _limiter, _hashCache, _configuration, _dispatcher,
                _dispatcherLimiter, _gameLocator, _dtos, _logger);
        });

        _modLists.Edit(lsts =>
        {
            lsts.Clear();
            lsts.AddOrUpdate(vms);
        });

        _logger.LogInformation("Loaded data for {Count} modlists", _modLists.Count);
    }

    private async Task LoadSettings()
    {
        try
        {
            if (SavedSettingsLocation.FileExists())
            {
                await using var stream = SavedSettingsLocation.Open(FileMode.Open);
                var data = (await JsonSerializer.DeserializeAsync<SavedSettings>(stream))!;
                SearchText = data.SearchText;

                SelectedGame = data.SelectedGame == null
                    ? null
                    : _gamesList.Lookup(data.SelectedGame.Value.MetaData().HumanFriendlyGameName).Value;

                ShowNSFW = data.ShowNSFW;
                OnlyUtilityLists = data.OnlyUtility;
                OnlyInstalledGames = data.OnlyInstalled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "While loading gallery browse settings");
        }
    }

    private async Task SaveSettings()
    {
        try
        {
            var settings = new SavedSettings
            {
                SearchText = SearchText,
                OnlyInstalled = OnlyInstalledGames,
                OnlyUtility = OnlyUtilityLists,
                ShowNSFW = ShowNSFW,
                SelectedGame = SelectedGame?.Game
            };
            SavedSettingsLocation.Parent.CreateDirectory();
            await using var stream = SavedSettingsLocation.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "While saving gallery browse settings");
        }
    }

    private class SavedSettings
    {
        public string SearchText { get; set; }
        public bool ShowNSFW { get; set; }
        public bool OnlyUtility { get; set; }
        public bool OnlyInstalled { get; set; }
        public Game? SelectedGame { get; set; }
    }
}