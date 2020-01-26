using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class VortexCompilerVM : ViewModel, ISubCompilerVM
    {
        public CompilerVM Parent { get; }

        private readonly VortexCompilationSettings _settings;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _modListSettings;
        public ModlistSettingsEditorVM ModlistSettings => _modListSettings.Value;

        private static readonly ObservableCollectionExtended<GameVM> _gameOptions = new ObservableCollectionExtended<GameVM>(
            EnumExt.GetValues<Game>()
                .Where(g => VortexCompiler.IsActiveVortexGame(g))
                .Select(g => new GameVM(g))
                .OrderBy(g => g.DisplayName));

        public ObservableCollectionExtended<GameVM> GameOptions => _gameOptions;

        [Reactive]
        public ACompiler ActiveCompilation { get; private set; }

        [Reactive]
        public GameVM SelectedGame { get; set; }

        [Reactive]
        public FilePickerVM GameLocation { get; set; }

        [Reactive]
        public FilePickerVM DownloadsLocation { get; set; }

        [Reactive]
        public FilePickerVM StagingLocation { get; set; }

        public ICommand FindGameInSteamCommand { get; }

        public ICommand FindGameInGogCommand { get; }

        [Reactive]
        public StatusUpdateTracker StatusTracker { get; private set; }

        public IObservable<bool> CanCompile { get; }

        public VortexCompilerVM(CompilerVM parent)
        {
            Parent = parent;
            GameLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Game Folder Location"
            };
            DownloadsLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Downloads Folder"
            };
            StagingLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Staging Folder"
            };

            // Load custom ModList settings when game type changes
            _modListSettings = (this).WhenAny(x => x.SelectedGame)
                .Select(game =>
                {
                    if (game == null) return null;
                    var gameSettings = _settings.ModlistSettings.TryCreate(game.Game);
                    return new ModlistSettingsEditorVM(gameSettings.ModlistSettings);
                })
                // Interject and save old while loading new
                .Pairwise()
                .Do(pair =>
                {
                    var (previous, current) = pair;
                    previous?.Save();
                    current?.Init();
                })
                .Select(x => x.Current)
                .ToGuiProperty(this, nameof(ModlistSettings));

            CanCompile = Observable.CombineLatest(
                    this.WhenAny(x => x.GameLocation.InError),
                    this.WhenAny(x => x.DownloadsLocation.InError),
                    this.WhenAny(x => x.StagingLocation.InError),
                    this.WhenAny(x => x.ModlistSettings)
                        .Select(x => x?.InError ?? Observable.Return(false))
                        .Switch(),
                    (g, d, s, ml) => !g && !d && !s && !ml)
                .Publish()
                .RefCount();

            // Load settings
            _settings = parent.MWVM.Settings.Compiler.VortexCompilation;
            SelectedGame = _gameOptions.FirstOrDefault(x => x.Game == _settings.LastCompiledGame) ?? _gameOptions[0];
            parent.MWVM.Settings.SaveSignal
                .Subscribe(_ => Unload())
                .DisposeWith(CompositeDisposable);

            // Load custom game settings when game type changes
            (this).WhenAny(x => x.SelectedGame)
                .Select(game => _settings.ModlistSettings.TryCreate(game.Game))
                .Pairwise()
                .Subscribe(pair =>
                {
                    // Save old
                    var (previous, current) = pair;
                    if (previous != null)
                    {
                        previous.GameLocation = GameLocation.TargetPath;
                    }

                    // Load new
                    GameLocation.TargetPath = current?.GameLocation;
                    if (string.IsNullOrWhiteSpace(GameLocation.TargetPath))
                    {
                        SetGameToSteamLocation();
                    }
                    if (string.IsNullOrWhiteSpace(GameLocation.TargetPath))
                    {
                        SetGameToGogLocation();
                    }
                    DownloadsLocation.TargetPath = current?.DownloadLocation;
                     if (string.IsNullOrWhiteSpace(DownloadsLocation.TargetPath))
                    {
                        DownloadsLocation.TargetPath = VortexCompiler.RetrieveDownloadLocation(SelectedGame.Game);
                    }
                    StagingLocation.TargetPath = current?.StagingLocation;
                    if (string.IsNullOrWhiteSpace(StagingLocation.TargetPath))
                    {
                        StagingLocation.TargetPath = VortexCompiler.RetrieveStagingLocation(SelectedGame.Game);
                    }
                })
                .DisposeWith(CompositeDisposable);

            // Find game commands
            FindGameInSteamCommand = ReactiveCommand.Create(SetGameToSteamLocation);
            FindGameInGogCommand = ReactiveCommand.Create(SetGameToGogLocation);

            // Add additional criteria to download/staging folders
            DownloadsLocation.AdditionalError = this.WhenAny(x => x.DownloadsLocation.TargetPath)
                .Select(path => path == null ? ErrorResponse.Success : VortexCompiler.IsValidDownloadsFolder(path));
            StagingLocation.AdditionalError = this.WhenAny(x => x.StagingLocation.TargetPath)
                .Select(path => path == null ? ErrorResponse.Success : VortexCompiler.IsValidBaseStagingFolder(path));
        }

        public void Unload()
        {
            _settings.LastCompiledGame = SelectedGame.Game;
            ModlistSettings?.Save();
        }

        private void SetGameToSteamLocation()
        {
            GameLocation.TargetPath = StoreHandler.Instance.GetGamePath(SelectedGame.Game, StoreType.STEAM);
        }

        private void SetGameToGogLocation()
        {
            GameLocation.TargetPath = StoreHandler.Instance.GetGamePath(SelectedGame.Game, StoreType.GOG);
        }

        public async Task<GetResponse<ModList>> Compile()
        {
            string outputFile = $"{ModlistSettings.ModListName}{Consts.ModListExtension}";
            if (!string.IsNullOrWhiteSpace(Parent.OutputLocation.TargetPath))
            {
                outputFile = Path.Combine(Parent.OutputLocation.TargetPath, outputFile);
            }
            try
            {
                using (ActiveCompilation = new VortexCompiler(
                    game: SelectedGame.Game,
                    gamePath: GameLocation.TargetPath,
                    vortexFolder: VortexCompiler.TypicalVortexFolder(),
                    downloadsFolder: DownloadsLocation.TargetPath,
                    stagingFolder: StagingLocation.TargetPath,
                    outputFile: outputFile)
                {
                    ModListName = ModlistSettings.ModListName,
                    ModListAuthor = ModlistSettings.AuthorText,
                    ModListDescription = ModlistSettings.Description,
                    ModListImage = ModlistSettings.ImagePath.TargetPath,
                    ModListWebsite = ModlistSettings.Website,
                    ModListReadme = ModlistSettings.ReadmeIsWebsite ? ModlistSettings.ReadmeWebsite : ModlistSettings.ReadmeFilePath.TargetPath,
                    ReadmeIsWebsite = ModlistSettings.ReadmeIsWebsite,
                })
                {
                    Parent.MWVM.Settings.Performance.AttachToBatchProcessor(ActiveCompilation);
                    var success = await ActiveCompilation.Begin();
                    return GetResponse<ModList>.Create(success, ActiveCompilation.ModList);
                }
            }
            finally
            {
                StatusTracker = null;
                ActiveCompilation.Dispose();
                ActiveCompilation = null;
            }
        }
    }
}
