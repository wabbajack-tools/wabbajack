using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerVM : ViewModel
    {
        public MainWindowVM MWVM { get; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _image;
        public BitmapImage Image => _image.Value;

        [Reactive]
        public ModManager SelectedCompilerType { get; set; }

        private readonly ObservableAsPropertyHelper<ISubCompilerVM> _compiler;
        public ISubCompilerVM Compiler => _compiler.Value;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _currentModlistSettings;
        public ModlistSettingsEditorVM CurrentModlistSettings => _currentModlistSettings.Value;

        private readonly ObservableAsPropertyHelper<bool> _compiling;
        public bool Compiling => _compiling.Value;

        private readonly ObservableAsPropertyHelper<float> _percentCompleted;
        public float PercentCompleted => _percentCompleted.Value;

        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();

        public ObservableCollectionExtended<string> Log => MWVM.Log;

        public IReactiveCommand BackCommand { get; }

        public CompilerVM(MainWindowVM mainWindowVM)
        {
            MWVM = mainWindowVM;

            // Load settings
            CompilerSettings settings = MWVM.Settings.Compiler;
            SelectedCompilerType = settings.LastCompiledModManager;
            MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.LastCompiledModManager = SelectedCompilerType;
                })
                .DisposeWith(CompositeDisposable);

            // Swap to proper sub VM based on selected type
            _compiler = this.WhenAny(x => x.SelectedCompilerType)
                // Delay so the initial VM swap comes in immediately, subVM comes right after
                .DelayInitial(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Select<ModManager, ISubCompilerVM>(type =>
                {
                    switch (type)
                    {
                        case ModManager.MO2:
                            return new MO2CompilerVM(this);
                        case ModManager.Vortex:
                            return new VortexCompilerVM(this);
                        default:
                            return null;
                    }
                })
                // Unload old VM
                .Pairwise()
                .Do(pair =>
                {
                    pair.Previous?.Unload();
                })
                .Select(p => p.Current)
                .ToProperty(this, nameof(Compiler));

            // Let sub VM determine what settings we're displaying and when
            _currentModlistSettings = this.WhenAny(x => x.Compiler.ModlistSettings)
                .ToProperty(this, nameof(CurrentModlistSettings));

            _image = this.WhenAny(x => x.CurrentModlistSettings.ImagePath.TargetPath)
                // Throttle so that it only loads image after any sets of swaps have completed
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .DistinctUntilChanged()
                .Select(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return UIUtils.BitmapImageFromResource("Resources/Wabba_Mouth_No_Text.png");
                    if (UIUtils.TryGetBitmapImageFromFile(path, out var image))
                    {
                        return image;
                    }
                    return null;
                })
                .ToProperty(this, nameof(Image));

            _compiling = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .Select(compilation => compilation != null)
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(Compiling));

            BackCommand = ReactiveCommand.Create(
                execute: () => mainWindowVM.ActivePane = mainWindowVM.ModeSelectionVM,
                canExecute: this.WhenAny(x => x.Compiling)
                    .Select(x => !x));

            // Compile progress updates and populate ObservableCollection
            this.WhenAny(x => x.Compiler.ActiveCompilation)
                .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>())
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .Filter(i => i.IsWorking)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            _percentCompleted = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .StartWith(default(ACompiler))
                .Pairwise()
                .Select(c =>
                {
                    if (c.Current == null)
                    {
                        return Observable.Return<float>(c.Previous == null ? 0f : 1f);
                    }
                    return c.Current.PercentCompleted;
                })
                .Switch()
                .Debounce(TimeSpan.FromMilliseconds(25))
                .ToProperty(this, nameof(PercentCompleted));
        }
    }
}
