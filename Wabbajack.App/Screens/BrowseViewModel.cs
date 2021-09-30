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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Controls;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.App.Screens
{
    public class BrowseViewModel : ViewModelBase, IActivatableViewModel
    {
        private readonly Client _wjClient;
        private readonly ILogger<BrowseViewModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly IResource<HttpClient> _limiter;
        private readonly FileHashCache _hashCache;
        private readonly Configuration _configuration;
        private readonly DownloadDispatcher _dispatcher;
        private readonly IResource<DownloadDispatcher> _dispatcherLimiter;

        private SourceCache<BrowseItemViewModel, string> _modLists = new(x => x.MachineURL);


        public readonly ReadOnlyObservableCollection<BrowseItemViewModel> _filteredModLists;
        public ReadOnlyObservableCollection<BrowseItemViewModel> ModLists => _filteredModLists;
        
        private SourceCache<GameSelectorItemViewModel, string> _gamesList = new(x => x.Name);
        public readonly ReadOnlyObservableCollection<GameSelectorItemViewModel> _filteredGamesList;
        private readonly GameLocator _gameLocator;
        private readonly DTOSerializer _dtos;
        public ReadOnlyObservableCollection<GameSelectorItemViewModel> GamesList => _filteredGamesList;
        
        [Reactive]
        public GameSelectorItemViewModel? SelectedGame { get; set; }
        
        [Reactive]
        public string SearchText { get; set; }

        [Reactive] public bool OnlyInstalledGames { get; set; } = false;

        [Reactive] public bool OnlyUtilityLists { get; set; } = false;

        [Reactive] public bool ShowNSFW { get; set; } = false;

        public BrowseViewModel(ILogger<BrowseViewModel> logger, Client wjClient, HttpClient httpClient, IResource<HttpClient> limiter, FileHashCache hashCache,
            IResource<DownloadDispatcher> dispatcherLimiter, DownloadDispatcher dispatcher, GameLocator gameLocator, DTOSerializer dtos, Configuration configuration)
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
            
            
            IObservable<Func<BrowseItemViewModel, bool>> searchTextPredicates = this.ObservableForProperty(vm => vm.SearchText)
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
                foreach (var game in GameRegistry.Games.Keys)
                {
                    e.AddOrUpdate(new GameSelectorItemViewModel(game));
                }
            });

            _gamesList.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(Comparer<GameSelectorItemViewModel>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)))
                .Bind(out _filteredGamesList)
                .Subscribe();
            
            IObservable<Func<BrowseItemViewModel, bool>> gameFilter = this.ObservableForProperty(vm => vm.SelectedGame)
                .Select(v => v.Value)
                .Select<GameSelectorItemViewModel?, Func<BrowseItemViewModel, bool>>(selected =>
                {
                    if (selected == null) return _ => true;
                    return item => item.Game == selected.Game;
                })
                .StartWith(_ => true);
            
            IObservable<Func<BrowseItemViewModel, bool>> onlyInstalledGamesFilter = this.ObservableForProperty(vm => vm.OnlyInstalledGames)
                .Select(v => v.Value)
                .Select<bool, Func<BrowseItemViewModel, bool>>(onlyInstalled =>
                {
                    if (onlyInstalled == false) return _ => true;
                    return item => _gameLocator.IsInstalled(item.Game);
                })
                .StartWith(_ => true);
            
            IObservable<Func<BrowseItemViewModel, bool>> onlyUtilityListsFilter = this.ObservableForProperty(vm => vm.OnlyUtilityLists)
                .Select(v => v.Value)
                .Select<bool, Func<BrowseItemViewModel, bool>>(utility =>
                {
                    if (utility == false) return item => item.IsUtilityList == false ;
                    return item => item.IsUtilityList;
                })
                .StartWith(item => item.IsUtilityList == false);
            
            IObservable<Func<BrowseItemViewModel, bool>> showNSFWFilter = this.ObservableForProperty(vm => vm.ShowNSFW)
                .Select(v => v.Value)
                .Select<bool, Func<BrowseItemViewModel, bool>>(showNSFW =>
                {
                    return item => item.IsNSFW == showNSFW;
                })
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

                Disposable.Create(() =>
                {
                    SaveSettings().FireAndForget();
                }).DisposeWith(disposables);

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

        [Reactive]
        public ReactiveCommand<Unit, Unit> ResetFiltersCommand { get; set; }

        private async Task LoadData()
        {
            var modlists = await _wjClient.LoadLists();
            var summaries = (await _wjClient.GetListStatuses()).ToDictionary(m => m.MachineURL);
            var vms = modlists.Select(m =>
            {
                if (!summaries.TryGetValue(m.Links.MachineURL, out var summary))
                {
                    summary = new ModListSummary();
                }

                return new BrowseItemViewModel(m, summary, _httpClient, _limiter, _hashCache, _configuration, _dispatcher, _dispatcherLimiter, _gameLocator, _dtos, _logger);
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
                    
                    SelectedGame = data.SelectedGame == null ? null : _gamesList.Lookup(data.SelectedGame.Value.MetaData().HumanFriendlyGameName).Value;

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

        private AbsolutePath SavedSettingsLocation => _configuration.SavedSettingsLocation.Combine("browse_view.json");

        private class SavedSettings
        {
            public string SearchText { get; set; }
            public bool ShowNSFW { get; set; }
            public bool OnlyUtility { get; set; }
            public bool OnlyInstalled { get; set; }
            public Game? SelectedGame { get; set; }
        }
    }
}