using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
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

        [Reactive]
        public Game SelectedGame { get; set; }

        [Reactive]
        public FilePickerVM GameLocation { get; set; }

        [Reactive]
        public FilePickerVM DownloadsLocation { get; set; }

        [Reactive]
        public FilePickerVM StagingLocation { get; set; }

        public VortexCompilerVM(CompilerVM parent)
        {
            this.GameLocation = new FilePickerVM()
            {
                DoExistsCheck = true,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Game Folder Location"
            };
            this.DownloadsLocation = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Downloads Folder"
            };
            this.StagingLocation = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Staging Folder"
            };

            // Wire start command
            this.BeginCommand = ReactiveCommand.CreateFromTask(
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
                            game: this.SelectedGame,
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
                            compiler.Compile();
                        }
                        catch (Exception ex)
                        {
                            while (ex.InnerException != null) ex = ex.InnerException;
                            Utils.Log($"Compiler error: {ex.ExceptionToString()}");
                        }
                    });
                });
            this._Compiling = this.BeginCommand.IsExecuting
                .ToProperty(this, nameof(this.Compiling));

            // Load settings
            this.settings = parent.MWVM.Settings.Compiler.VortexCompilation;
            this.SelectedGame = settings.LastCompiledGame;
            if (!string.IsNullOrWhiteSpace(settings.DownloadLocation))
            {
                this.DownloadsLocation.TargetPath = settings.DownloadLocation;
            }
            if (!string.IsNullOrWhiteSpace(settings.StagingLocation))
            {
                this.StagingLocation.TargetPath = settings.StagingLocation;
            }
            parent.MWVM.Settings.SaveSignal
                .Subscribe(_ => Unload())
                .DisposeWith(this.CompositeDisposable);

            // Load custom game settings when game type changes
            this.WhenAny(x => x.SelectedGame)
                .Select(game => settings.ModlistSettings.TryCreate(game))
                .Pairwise()
                .Subscribe(pair =>
                {
                    if (pair.Previous != null)
                    {
                        pair.Previous.GameLocation = this.GameLocation.TargetPath;
                    }
                    this.GameLocation.TargetPath = pair.Current?.GameLocation ?? null;
                })
                .DisposeWith(this.CompositeDisposable);

            // Load custom modlist settings when game type changes
            this._ModlistSettings = this.WhenAny(x => x.SelectedGame)
                .Select(game =>
                {
                    var gameSettings = settings.ModlistSettings.TryCreate(game);
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
        }

        public void Unload()
        {
            settings.DownloadLocation = this.DownloadsLocation.TargetPath;
            settings.StagingLocation = this.StagingLocation.TargetPath;
            settings.LastCompiledGame = this.SelectedGame;
            this.ModlistSettings?.Save();
        }
    }
}