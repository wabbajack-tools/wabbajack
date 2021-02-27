using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public class ModListGalleryVM : BackNavigatingVM
    {
        public MainWindowVM MWVM { get; }

        public ObservableCollectionExtended<ModListMetadataVM> ModLists { get; } = new ObservableCollectionExtended<ModListMetadataVM>();

        private const string ALL_GAME_TYPE = "All";

        [Reactive]
        public IErrorResponse Error { get; set; }

        [Reactive]
        public string Search { get; set; }

        [Reactive]
        public bool OnlyInstalled { get; set; }

        [Reactive]
        public bool ShowNSFW { get; set; }

        [Reactive]
        public bool ShowUtilityLists { get; set; }

        [Reactive]
        public string GameType { get; set; }

        public List<string> GameTypeEntries { get { return GetGameTypeEntries(); } }

        private readonly ObservableAsPropertyHelper<bool> _Loaded;

        private FiltersSettings settings => MWVM.Settings.Filters;

        public bool Loaded => _Loaded.Value;

        public ICommand ClearFiltersCommand { get; }

        public ModListGalleryVM(MainWindowVM mainWindowVM)
            : base(mainWindowVM)
        {
            MWVM = mainWindowVM;

            // load persistent filter settings
            if (settings.IsPersistent)
            {
                GameType = !string.IsNullOrEmpty(settings.Game) ? settings.Game : ALL_GAME_TYPE;
                ShowNSFW = settings.ShowNSFW;
                ShowUtilityLists = settings.ShowUtilityLists;
                OnlyInstalled = settings.OnlyInstalled;
                Search = settings.Search;
            }
            else
                GameType = ALL_GAME_TYPE;

            // subscribe to save signal
            MWVM.Settings.SaveSignal
                .Subscribe(_ => UpdateFiltersSettings())
                .DisposeWith(this.CompositeDisposable);

            ClearFiltersCommand = ReactiveCommand.Create(
                () =>
                {
                    OnlyInstalled = false;
                    ShowNSFW = false;
                    ShowUtilityLists = false;
                    Search = string.Empty;
                    GameType = ALL_GAME_TYPE;
                });


            this.WhenAny(x => x.OnlyInstalled)
                .Subscribe(val =>
                {
                    if(val)
                        GameType = ALL_GAME_TYPE;
                })
                .DisposeWith(CompositeDisposable);

            var sourceList = Observable.Return(Unit.Default)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async _ =>
                {
                    try
                    {
                        Error = null;
                        var list = await ModlistMetadata.LoadFromGithub();
                        Error = ErrorResponse.Success;
                        return list
                            .AsObservableChangeSet(x => x.DownloadMetadata?.Hash ?? Hash.Empty);
                    }
                    catch (Exception ex)
                    {
                        Utils.Error(ex);
                        Error = ErrorResponse.Fail(ex);
                        return Observable.Empty<IChangeSet<ModlistMetadata, Hash>>();
                    }
                })
                // Unsubscribe and release when not active
                .FlowSwitch(
                    this.WhenAny(x => x.IsActive),
                    valueWhenOff: Observable.Return(ChangeSet<ModlistMetadata, Hash>.Empty))
                .Switch()
                .RefCount();

            _Loaded = sourceList.CollectionCount()
                .Select(c => c > 0)
                .ToProperty(this, nameof(Loaded));

            // Convert to VM and bind to resulting list
            sourceList
                .ObserveOnGuiThread()
                .Transform(m => new ModListMetadataVM(this, m))
                .DisposeMany()
                // Filter only installed
                .Filter(this.WhenAny(x => x.OnlyInstalled)
                    .Select<bool, Func<ModListMetadataVM, bool>>(onlyInstalled => (vm) =>
                    {
                        if (!onlyInstalled) return true;
                        if (!GameRegistry.Games.TryGetValue(vm.Metadata.Game, out var gameMeta)) return false;
                        return gameMeta.IsInstalled;
                    }))
                // Filter on search box
                .Filter(this.WhenAny(x => x.Search)
                    .Debounce(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler)
                    .Select<string, Func<ModListMetadataVM, bool>>(search => (vm) =>
                    {
                        if (string.IsNullOrWhiteSpace(search)) return true;
                        return vm.Metadata.Title.ContainsCaseInsensitive(search);
                    }))
                .Filter(this.WhenAny(x => x.ShowNSFW)
                    .Select<bool, Func<ModListMetadataVM, bool>>(showNSFW => vm =>
                    {
                        if (!vm.Metadata.NSFW) return true;
                        return vm.Metadata.NSFW && showNSFW;
                    }))
                .Filter(this.WhenAny(x => x.ShowUtilityLists)
                    .Select<bool, Func<ModListMetadataVM, bool>>(showUtilityLists => vm => showUtilityLists ? vm.Metadata.UtilityList : !vm.Metadata.UtilityList))
                // Filter by Game
                .Filter(this.WhenAny(x => x.GameType)
                    .Debounce(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler)
                    .Select<string, Func<ModListMetadataVM, bool>>(GameType => (vm) =>
                    {
                        if (GameType == ALL_GAME_TYPE)
                            return true;
                        if (string.IsNullOrEmpty(GameType))
                            return false;

                        return GameType == vm.Metadata.Game.GetDescription<Game>().ToString();

                    }))
                .Bind(ModLists)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            // Extra GC when navigating away, just to immediately clean up modlist metadata
            this.WhenAny(x => x.IsActive)
                .Where(x => !x)
                .Skip(1)
                .Delay(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    GC.Collect();
                })
                .DisposeWith(CompositeDisposable);
        }

        public override void Unload()
        {
            Error = null;
        }

        private List<string> GetGameTypeEntries()
        {
            List<string> gameEntries = new List<string> { ALL_GAME_TYPE };
            gameEntries.AddRange(EnumExtensions.GetAllItems<Game>().Select(gameType => gameType.GetDescription<Game>()));
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
