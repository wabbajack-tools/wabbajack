using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class VortexCompilerVM : ViewModel, ISubCompilerVM
    {
        private readonly VortexCompilationSettings _settings;

        public IReactiveCommand BeginCommand { get; }

        private readonly ObservableAsPropertyHelper<bool> _compiling;
        public bool Compiling => _compiling.Value;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _modListSettings;
        public ModlistSettingsEditorVM ModlistSettings => _modListSettings.Value;

        private static readonly ObservableCollectionExtended<GameVM> _gameOptions = new ObservableCollectionExtended<GameVM>(
            EnumExt.GetValues<Game>()
                .Where(g => GameRegistry.Games[g].SupportedModManager == ModManager.Vortex)
                .Select(g => new GameVM(g))
                .OrderBy(g => g.DisplayName));

        public ObservableCollectionExtended<GameVM> GameOptions => _gameOptions;

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

        public VortexCompilerVM(CompilerVM parent)
        {
            GameLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Game Folder Location"
            };
            DownloadsLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Downloads Folder"
            };
            StagingLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Staging Folder"
            };

            // Wire start command
            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: Observable.CombineLatest(
                        this.WhenAny(x => x.GameLocation.InError),
                        this.WhenAny(x => x.DownloadsLocation.InError),
                        this.WhenAny(x => x.StagingLocation.InError),
                        (g, d, s) => !g && !d && !s)
                    .ObserveOnGuiThread(),
                execute: async () =>
                {
                    VortexCompiler compiler;
                    try
                    {
                        compiler = new VortexCompiler(
                            SelectedGame.Game,
                            GameLocation.TargetPath,
                            VortexCompiler.TypicalVortexFolder(),
                            DownloadsLocation.TargetPath,
                            StagingLocation.TargetPath)
                        {
                            ModListName = ModlistSettings.ModListName,
                            ModListAuthor = ModlistSettings.AuthorText,
                            ModListDescription = ModlistSettings.Description,
                            ModListImage = ModlistSettings.ImagePath.TargetPath,
                            ModListWebsite = ModlistSettings.Website,
                            ModListReadme = ModlistSettings.ReadMeText.TargetPath
                        };
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log($"Compiler error: {ex.ExceptionToString()}");
                        return;
                    }
                    await Task.Run(() =>
                    {
                        try
                        {
                            this.StatusTracker = compiler.UpdateTracker;
                            compiler.Compile();
                        }
                        catch (Exception ex)
                        {
                            while (ex.InnerException != null) ex = ex.InnerException;
                            Utils.Log($"Compiler error: {ex.ExceptionToString()}");
                        }
                        finally
                        {
                            this.StatusTracker = null;
                        }
                    });
                });
            _compiling = BeginCommand.IsExecuting
                .ToProperty(this, nameof(Compiling));

            // Load settings
            _settings = parent.MWVM.Settings.Compiler.VortexCompilation;
            SelectedGame = _gameOptions.FirstOrDefault(x => x.Game == _settings.LastCompiledGame) ?? _gameOptions[0];
            parent.MWVM.Settings.SaveSignal
                .Subscribe(_ => Unload())
                .DisposeWith(CompositeDisposable);

            // Load custom game settings when game type changes
            this.WhenAny(x => x.SelectedGame)
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

            // Load custom ModList settings when game type changes
            this._modListSettings = this.WhenAny(x => x.SelectedGame)
                .Select(game =>
                {
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
                // Save to property
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(ModlistSettings));

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
            var steamGame = SteamHandler.Instance.Games.FirstOrDefault(g => g.Game.HasValue && g.Game == SelectedGame.Game);
            GameLocation.TargetPath = steamGame?.InstallDir;
        }

        private void SetGameToGogLocation()
        {
            var gogGame = GOGHandler.Instance.Games.FirstOrDefault(g => g.Game.HasValue && g.Game == SelectedGame.Game);
            GameLocation.TargetPath = gogGame?.Path;
        }
    }
}