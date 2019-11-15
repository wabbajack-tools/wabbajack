using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
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

        public CompilerVM(MainWindowVM mainWindowVM)
        {
            this.MWVM = mainWindowVM;

            // Load settings
            CompilerSettings settings = this.MWVM.Settings.Compiler;
            this.SelectedCompilerType = settings.LastCompiledModManager;
            this.MWVM.Settings.SaveSignal
                .Subscribe(_ =>
                {
                    settings.LastCompiledModManager = this.SelectedCompilerType;
                })
                .DisposeWith(this.CompositeDisposable);

            // Swap to proper sub VM based on selected type
            this._Compiler = this.WhenAny(x => x.SelectedCompilerType)
                // Delay so the initial VM swap comes in immediately, subVM comes right after
                .DelayInitial(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Select<ModManager, ISubCompilerVM>(type =>
                {
                    switch (type)
                    {
                        case ModManager.MO2:
                            return new MO2CompilerVM(this);
                        case ModManager.Vortex:
                            return new VortexCompilerVM();
                        default:
                            return null;
                    }
                })
                .ToProperty(this, nameof(this.Compiler));

            // Let sub VM determine what settings we're displaying and when
            this._CurrentModlistSettings = this.WhenAny(x => x.Compiler.ModlistSettings)
                .ToProperty(this, nameof(this.CurrentModlistSettings));

            this._Image = this.WhenAny(x => x.CurrentModlistSettings.ImagePath.TargetPath)
                // Delay so the initial VM swap comes in immediately, image comes right after
                .DelayInitial(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Select(path =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return UIUtils.BitmapImageFromResource("Wabbajack.Resources.Banner_Dark.png");
                    if (UIUtils.TryGetBitmapImageFromFile(path, out var image))
                    {
                        return image;
                    }
                    return UIUtils.BitmapImageFromResource("Wabbajack.Resources.none.png");
                })
                .ToProperty(this, nameof(this.Image));
        }
    }
}
