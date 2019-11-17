using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
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
        private readonly VortexCompilationSettings settings;

        public IReactiveCommand BeginCommand { get; }

        private readonly ObservableAsPropertyHelper<bool> _Compiling;
        public bool Compiling => _Compiling.Value;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _ModlistSettings;
        public ModlistSettingsEditorVM ModlistSettings => _ModlistSettings.Value;

        private static ObservableCollectionExtended<GameVM> gameOptions = new ObservableCollectionExtended<GameVM>(
            EnumExt.GetValues<Game>()
                .Where(g => GameRegistry.Games[g].SupportedModManager == ModManager.Vortex)
                .Select(g => new GameVM(g))
                .OrderBy(g => g.DisplayName));

        public ObservableCollectionExtended<GameVM> GameOptions => gameOptions;

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
                        resultSelector: (g, d, s) => !g && !d && !s)
                    .ObserveOnGuiThread(),
                execute: async () =>
                {
                    VortexCompiler compiler;
                    try
                    {
                        compiler = new VortexCompiler(
                            game: this.SelectedGame.Game,
                            gamePath: this.GameLocation.TargetPath,
                            vortexFolder: VortexCompiler.TypicalVortexFolder(),
                            downloadsFolder: this.DownloadsLocation.TargetPath,
                            stagingFolder: this.StagingLocation.TargetPath);
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
            _Compiling = this.BeginCommand.IsExecuting
                .ToProperty(this, nameof(this.Compiling));

            // Load settings
            settings = parent.MWVM.Settings.Compiler.VortexCompilation;
            SelectedGame = gameOptions.FirstOrDefault(x => x.Game == settings.LastCompiledGame) ?? gameOptions[0];
            parent.MWVM.Settings.SaveSignal
                .Subscribe(_ => Unload())
                .DisposeWith(this.CompositeDisposable);

            // Load custom game settings when game type changes
            this.WhenAny(x => x.SelectedGame)
                .Select(game => settings.ModlistSettings.TryCreate(game.Game))
                .Pairwise()
                .Subscribe(pair =>
                {
                    // Save old
                    if (pair.Previous != null)
                    {
                        pair.Previous.GameLocation = this.GameLocation.TargetPath;
                    }

                    // Load new
                    this.GameLocation.TargetPath = pair.Current?.GameLocation ?? null;
                    if (string.IsNullOrWhiteSpace(this.GameLocation.TargetPath))
                    {
                        this.SetGameToSteamLocation();
                    }
                    if (string.IsNullOrWhiteSpace(this.GameLocation.TargetPath))
                    {
                        this.SetGameToGogLocation();
                    }
                    this.DownloadsLocation.TargetPath = pair.Current?.DownloadLocation ?? null;
                     if (string.IsNullOrWhiteSpace(this.DownloadsLocation.TargetPath))
                    {
                        this.DownloadsLocation.TargetPath = VortexCompiler.RetrieveDownloadLocation(this.SelectedGame.Game);
                    }
                    this.StagingLocation.TargetPath = pair.Current?.StagingLocation ?? null;
                    if (string.IsNullOrWhiteSpace(this.StagingLocation.TargetPath))
                    {
                        this.StagingLocation.TargetPath = VortexCompiler.RetrieveStagingLocation(this.SelectedGame.Game);
                    }
                })
                .DisposeWith(this.CompositeDisposable);

            // Load custom modlist settings when game type changes
            this._ModlistSettings = this.WhenAny(x => x.SelectedGame)
                .Select(game =>
                {
                    var gameSettings = settings.ModlistSettings.TryCreate(game.Game);
                    return new ModlistSettingsEditorVM(gameSettings.ModlistSettings);
                })
                // Interject and save old while loading new
                .Pairwise()
                .Do(pair =>
                {
                    pair.Previous?.Save();
                    pair.Current?.Init();
                })
                .Select(x => x.Current)
                // Save to property
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(this.ModlistSettings));

            // Find game commands
            this.FindGameInSteamCommand = ReactiveCommand.Create(SetGameToSteamLocation);
            this.FindGameInGogCommand = ReactiveCommand.Create(SetGameToGogLocation);

            // Add additional criteria to download/staging folders
            this.DownloadsLocation.AdditionalError = this.WhenAny(x => x.DownloadsLocation.TargetPath)
                .Select(path =>
                {
                    if (path == null) return ErrorResponse.Success;
                    return VortexCompiler.IsValidDownloadsFolder(path);
                });
            this.StagingLocation.AdditionalError = this.WhenAny(x => x.StagingLocation.TargetPath)
                .Select(path =>
                {
                    if (path == null) return ErrorResponse.Success;
                    return VortexCompiler.IsValidBaseStagingFolder(path);
                });
        }

        public void Unload()
        {
            settings.LastCompiledGame = this.SelectedGame.Game;
            this.ModlistSettings?.Save();
        }

        private void SetGameToSteamLocation()
        {
            var steamGame = SteamHandler.Instance.Games.FirstOrDefault(g => g.Game.HasValue && g.Game == this.SelectedGame.Game);
            this.GameLocation.TargetPath = steamGame?.InstallDir;
        }

        private void SetGameToGogLocation()
        {
            var gogGame = GOGHandler.Instance.Games.FirstOrDefault(g => g.Game.HasValue && g.Game == this.SelectedGame.Game);
            this.GameLocation.TargetPath = gogGame?.Path;
        }
    }
}