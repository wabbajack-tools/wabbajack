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

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        [Reactive]
        public ModManager SelectedCompilerType { get; set; }

        private readonly ObservableAsPropertyHelper<ISubCompilerVM> _Compiler;
        public ISubCompilerVM Compiler => _Compiler.Value;

        private readonly ObservableAsPropertyHelper<ModlistSettingsEditorVM> _CurrentModlistSettings;
        public ModlistSettingsEditorVM CurrentModlistSettings => _CurrentModlistSettings.Value;

        private readonly ObservableAsPropertyHelper<StatusUpdateTracker> _CurrentStatusTracker;
        public StatusUpdateTracker CurrentStatusTracker => _CurrentStatusTracker.Value;

        private readonly ObservableAsPropertyHelper<bool> _Compiling;
        public bool Compiling => _Compiling.Value;

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
            _Compiler = this.WhenAny(x => x.SelectedCompilerType)
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
            _CurrentModlistSettings = this.WhenAny(x => x.Compiler.ModlistSettings)
                .ToProperty(this, nameof(CurrentModlistSettings));

            // Let sub VM determine what progress we're seeing
            _CurrentStatusTracker = this.WhenAny(x => x.Compiler.StatusTracker)
                .ToProperty(this, nameof(CurrentStatusTracker));

            _Image = this.WhenAny(x => x.CurrentModlistSettings.ImagePath.TargetPath)
                // Throttle so that it only loads image after any sets of swaps have completed
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .DistinctUntilChanged()
                .Select(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");
                    if (UIUtils.TryGetBitmapImageFromFile(path, out var image))
                    {
                        return image;
                    }
                    return null;
                })
                .ToProperty(this, nameof(Image));

            _Compiling = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .Select(compilation => compilation != null)
                .ObserveOnGuiThread()
                .ToProperty(this, nameof(Compiling));

            // Compile progress updates and populate ObservableCollection
            var subscription = this.WhenAny(x => x.Compiler.ActiveCompilation)
                .SelectMany(c => c?.QueueStatus ?? Observable.Empty<CPUStatus>())
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID), SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(MWVM.StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
        }
    }
}
