using System;
using System.Reactive.Disposables;
using ReactiveUI;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Media.Imaging;
using DynamicData;
using DynamicData.Binding;
using Wabbajack.Common;
using Wabbajack.Lib;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack
{
    public class InstallerVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        private readonly ObservableAsPropertyHelper<int> _queueProgress;
        public int QueueProgress => _queueProgress.Value;

        public ObservableCollectionExtended<CPUStatus> StatusList { get; } = new ObservableCollectionExtended<CPUStatus>();

        public ObservableCollectionExtended<string> Log { get; } = new ObservableCollectionExtended<string>();

        public SlideShow Slideshow { get; }

        public ModListVM ModList { get; set; }
        public string InstallPath { get; set; }
        public string DownloadPath { get; set; }

        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");

        [Reactive]
        public bool Installing { get; set; }

        private readonly ObservableAsPropertyHelper<float> _progressPercent;
        public float ProgressPercent => _progressPercent.Value;

        private readonly ObservableAsPropertyHelper<BitmapImage> _image;
        public BitmapImage Image => _image.Value;

        private readonly ObservableAsPropertyHelper<string> _titleText;
        public string TitleText => _titleText.Value;

        private readonly ObservableAsPropertyHelper<string> _authorText;
        public string AuthorText => _authorText.Value;

        private readonly ObservableAsPropertyHelper<string> _description;
        public string Description => _description.Value;

        public InstallerVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;

            Utils.LogMessages
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250))
                .Where(l => l.Count > 0)
                .FlattenBufferResult()
                .Top(5000)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(Log)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
            Utils.StatusUpdates
                .Subscribe((i) => WorkQueue.Report(i.Message, i.Progress))
                .DisposeWith(CompositeDisposable);

            WorkQueue.Status
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(250))
                .EnsureUniqueChanges()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Sort(SortExpressionComparer<CPUStatus>.Ascending(s => s.ID),
                    SortOptimisations.ComparesImmutableValuesOnly)
                .Bind(StatusList)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            _queueProgress = WorkQueue.QueueSize
                .Select(progress =>
                {
                    var (current, max) = progress;
                    if (max == 0)
                        max = 1;
                    return current * 100 / max;
                }).ToProperty(this, nameof(QueueProgress));

            /*this.MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.InstallationLocation = this.Location;
                    settings.DownloadLocation = this.DownloadLocation;
                })
                .DisposeWith(this.CompositeDisposable);*/

            _progressPercent = this.WhenAny(x => x.Installing).Select(show => show ? 1f : 0f)
                .ToProperty(this, nameof(ProgressPercent));
                // Disable for now, until more reliable
                //this.WhenAny(x => x.MWVM.QueueProgress)
                //    .Select(i => i / 100f)

            Slideshow = new SlideShow(this);

            // Set display items to modlist if configuring or complete,
            // or to the current slideshow data if installing
            _image = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList)
                        .SelectMany(x => x?.ImageObservable ?? Observable.Empty<BitmapImage>())
                        .NotNull()
                        .StartWith(WabbajackLogo),
                    this.WhenAny(x => x.Slideshow.Image)
                        .StartWith(default(BitmapImage)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, slideshow, installing) => installing ? slideshow : modList)
                .ToProperty(this, nameof(this.Image));
            _titleText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Name),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModName)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.TitleText));
            _authorText = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Author),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModAuthor)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.AuthorText));
            _description = Observable.CombineLatest(
                    this.WhenAny(x => x.ModList.Description),
                    this.WhenAny(x => x.Slideshow.TargetMod.ModDescription)
                        .StartWith(default(string)),
                    this.WhenAny(x => x.Installing),
                    resultSelector: (modList, mod, installing) => installing ? mod : modList)
                .ToProperty(this, nameof(this.Description));

            //ExecuteBegin();
        }

        private void ExecuteBegin()
        {
            Installing = true;
            var installer = new Installer(ModList.ModListPath, ModList.SourceModList, InstallPath)
            {
                DownloadFolder = DownloadPath
            };
            var th = new Thread(() =>
            {
                try
                {
                    installer.Install();
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    Utils.Log(ex.StackTrace);
                    Utils.Log(ex.ToString());
                    Utils.Log($"{ex.Message} - Can't continue");
                }
                finally
                {

                    Installing = false;
                }
            })
            {
                Priority = ThreadPriority.BelowNormal
            };
            th.Start();
        }
    }
}