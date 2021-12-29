

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Lib.Extensions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack
{
    public class ModListGalleryVM : BackNavigatingVM
    {
        public MainWindowVM MWVM { get; }
        
        private readonly SourceCache<ModListMetadataVM, string> _modLists = new(x => x.Metadata.Links.MachineURL);
        public ReadOnlyObservableCollection<ModListMetadataVM> _filteredModLists;
        public ReadOnlyObservableCollection<ModListMetadataVM> ModLists => _filteredModLists;

        private const string ALL_GAME_TYPE = "All";

        [Reactive] public IErrorResponse Error { get; set; }

        [Reactive] public string Search { get; set; }

        [Reactive] public bool OnlyInstalled { get; set; }

        [Reactive] public bool ShowNSFW { get; set; }

        [Reactive] public bool ShowUtilityLists { get; set; }

        [Reactive] public string GameType { get; set; }

        public List<string> GameTypeEntries => GetGameTypeEntries();

        private ObservableAsPropertyHelper<bool> _Loaded;
        private readonly Client _wjClient;
        private readonly ILogger<ModListGalleryVM> _logger;
        private readonly GameLocator _locator;
        private readonly ModListDownloadMaintainer _maintainer;

        private FiltersSettings settings { get; set; } = new();

        public bool Loaded => _Loaded.Value;

        public ICommand ClearFiltersCommand { get; set; }

        public ModListGalleryVM(ILogger<ModListGalleryVM> logger, Client wjClient,
            GameLocator locator, SettingsManager settingsManager, ModListDownloadMaintainer maintainer)
            : base(logger)
        {
            _wjClient = wjClient;
            _logger = logger;
            _locator = locator;
            _maintainer = maintainer;
            
            ClearFiltersCommand = ReactiveCommand.Create(
                () =>
                {
                    OnlyInstalled = false;
                    ShowNSFW = false;
                    ShowUtilityLists = false;
                    Search = string.Empty;
                    GameType = ALL_GAME_TYPE;
                });


            this.WhenActivated(disposables =>
            {
                var _ = LoadModLists();
                
                _modLists.Connect()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Bind(out _filteredModLists)
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        public override void Unload()
        {
            Error = null;
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
                        new ModListMetadataVM(_logger, this, m, _maintainer, _wjClient)));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While loading lists");
                ll.Fail();
            }
            ll.Succeed();
        }

        private List<string> GetGameTypeEntries()
        {
            List<string> gameEntries = new List<string> {ALL_GAME_TYPE};
            gameEntries.AddRange(GameRegistry.Games.Values.Select(gameType => gameType.HumanFriendlyGameName));
            gameEntries.Sort();
            return gameEntries;
        }

        private void UpdateFiltersSettings()
        {
            settings.Game = GameType;
            settings.Search = Search;
            settings.ShowNSFW = ShowNSFW;
            settings.ShowUtilityLists = ShowUtilityLists;
            settings.OnlyInstalled = OnlyInstalled;
        }
    }
}