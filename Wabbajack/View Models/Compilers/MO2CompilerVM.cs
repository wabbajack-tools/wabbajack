using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class MO2CompilerVM : ViewModel, ISubCompilerVM
    {
        private readonly MO2CompilationSettings settings;

        private readonly ObservableAsPropertyHelper<string> _Mo2Folder;
        public string Mo2Folder => _Mo2Folder.Value;

        private readonly ObservableAsPropertyHelper<string> _MOProfile;
        public string MOProfile => _MOProfile.Value;

        public FilePickerVM DownloadLocation { get; }

        public FilePickerVM ModlistLocation { get; }

        public IReactiveCommand BeginCommand { get; }

        [Reactive]
        public ACompiler ActiveCompilation { get; private set; }

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _ModlistSettings;
        public ModlistSettingsEditorVM ModlistSettings => _ModlistSettings.Value;

        [Reactive]
        public StatusUpdateTracker StatusTracker { get; private set; }

        public MO2CompilerVM(CompilerVM parent)
        {
            ModlistLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select Modlist"
            };
            DownloadLocation = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Select Download Location",
            };

            _Mo2Folder = this.WhenAny(x => x.ModlistLocation.TargetPath)
                .Select(loc =>
                {
                    try
                    {
                        var profile_folder = Path.GetDirectoryName(loc);
                        return Path.GetDirectoryName(Path.GetDirectoryName(profile_folder));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                })
                .ToProperty(this, nameof(Mo2Folder));
            _MOProfile = this.WhenAny(x => x.ModlistLocation.TargetPath)
                .Select(loc =>
                {
                    try
                    {
                        var profile_folder = Path.GetDirectoryName(loc);
                        return Path.GetFileName(profile_folder);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                })
                .ToProperty(this, nameof(MOProfile));

            // Wire missing Mo2Folder to signal error state for Modlist Location
            ModlistLocation.AdditionalError = this.WhenAny(x => x.Mo2Folder)
                .Select<string, IErrorResponse>(moFolder =>
                {
                    if (Directory.Exists(moFolder)) return ErrorResponse.Success;
                    return ErrorResponse.Fail($"MO2 Folder could not be located from the given modlist location.{Environment.NewLine}Make sure your modlist is inside a valid MO2 distribution.");
                });

            // Wire start command
            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: Observable.CombineLatest(
                        this.WhenAny(x => x.ModlistLocation.InError),
                        this.WhenAny(x => x.DownloadLocation.InError),
                        resultSelector: (ml, down) => !ml && !down)
                    .ObserveOnGuiThread(),
                execute: async () =>
                {
                    try
                    {
                        ActiveCompilation = new MO2Compiler(Mo2Folder)
                        {
                            MO2Profile = MOProfile,
                            ModListName = ModlistSettings.ModListName,
                            ModListAuthor = ModlistSettings.AuthorText,
                            ModListDescription = ModlistSettings.Description,
                            ModListImage = ModlistSettings.ImagePath.TargetPath,
                            ModListWebsite = ModlistSettings.Website,
                            ModListReadme = ModlistSettings.ReadMeText.TargetPath,
                        };
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log($"Compiler error: {ex.ExceptionToString()}");
                        return;
                    }

                    try
                    {
                        await ActiveCompilation.Begin();
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log($"Compiler error: {ex.ExceptionToString()}");
                    }
                    finally
                    {
                        StatusTracker = null;
                        ActiveCompilation.Dispose();
                        ActiveCompilation = null;
                    }
                    
                });

            // Load settings
            settings = parent.MWVM.Settings.Compiler.MO2Compilation;
            ModlistLocation.TargetPath = settings.LastCompiledProfileLocation;
            if (!string.IsNullOrWhiteSpace(settings.DownloadLocation))
            {
                DownloadLocation.TargetPath = settings.DownloadLocation;
            }
            parent.MWVM.Settings.SaveSignal
                .Subscribe(_ => Unload())
                .DisposeWith(CompositeDisposable);

            // Load custom modlist settings per MO2 profile
            _ModlistSettings = Observable.CombineLatest(
                    this.WhenAny(x => x.ModlistLocation.ErrorState),
                    this.WhenAny(x => x.ModlistLocation.TargetPath),
                    resultSelector: (State, Path) => (State, Path))
                // A short throttle is a quick hack to make the above changes "atomic"
                .Throttle(TimeSpan.FromMilliseconds(25))
                .Select(u =>
                {
                    if (u.State.Failed) return null;
                    var modlistSettings = settings.ModlistSettings.TryCreate(u.Path);
                    return new ModlistSettingsEditorVM(modlistSettings)
                    {
                        ModListName = MOProfile
                    };
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
                .ToProperty(this, nameof(ModlistSettings));

            // If Mo2 folder changes and download location is empty, set it for convenience
            this.WhenAny(x => x.Mo2Folder)
                .DelayInitial(TimeSpan.FromMilliseconds(100))
                .Where(x => Directory.Exists(x))
                .FilterSwitch(
                    this.WhenAny(x => x.DownloadLocation.Exists)
                        .Invert())
                .Subscribe(x =>
                {
                    try
                    {
                        var tmp_compiler = new MO2Compiler(Mo2Folder);
                        DownloadLocation.TargetPath = tmp_compiler.MO2DownloadsFolder;
                    }
                    catch (Exception ex)
                    {
                        Utils.Log($"Error setting default download location {ex}");
                    }
                })
                .DisposeWith(CompositeDisposable);
        }

        public void Unload()
        {
            settings.DownloadLocation = DownloadLocation.TargetPath;
            settings.LastCompiledProfileLocation = ModlistLocation.TargetPath;
            ModlistSettings?.Save();
        }
    }
}
