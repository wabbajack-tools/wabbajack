using System;
using System.Reactive.Linq;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerConfigVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        private readonly ObservableAsPropertyHelper<ViewModel> _configArea;
        public ViewModel ConfigArea => _configArea.Value;

        private readonly Lazy<MO2CompilerConfigVM> _mo2CompilerConfig;

        public BitmapImage MO2Image => UIUtils.BitmapImageFromResource("Wabbajack.Resources.MO2Button.png");
        public BitmapImage VortexImage => UIUtils.BitmapImageFromResource("Wabbajack.Resources.VortexButton.png");

        [Reactive]
        public Effect MO2Effect { get; set; }
        [Reactive]
        public Effect VortexEffect { get; set; }

        private readonly Effect StdBlur = new BlurEffect{Radius = 6};

        [Reactive]
        public bool ModManager { get; set; }

        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand UseMO2Command { get; }
        public IReactiveCommand UseVortexCommand { get; }

        public CompilerConfigVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;

            _mo2CompilerConfig = new Lazy<MO2CompilerConfigVM>(() => new MO2CompilerConfigVM(this));

            MO2Effect = null;
            VortexEffect = null;

            BackCommand = ReactiveCommand.Create(() => { _mainWindow.CurrentPage = Page.StartUp; });
            UseMO2Command = ReactiveCommand.Create(() =>
            {
                SwapEffects(true);
                ModManager = true;
            });
            UseVortexCommand = ReactiveCommand.Create(() =>
            {
                SwapEffects(false);
                ModManager = false;
            });

            _configArea = this.WhenAny(x => x.ModManager).Select<bool, ViewModel>(a => a == false ? default : _mo2CompilerConfig.Value).ToProperty(this, nameof(ConfigArea));
        }

        public void Compile(string source)
        {
            _mainWindow.Compile(source);
        }

        // swaps the blur between the MO2 and Vortex button
        private void SwapEffects(bool b)
        {
            if(MO2Effect == null && VortexEffect == null)
                if (b)
                    VortexEffect = StdBlur;
                else
                    MO2Effect = StdBlur;
            else
            {
                if (MO2Effect == StdBlur && b)
                {
                    MO2Effect = null;
                    VortexEffect = StdBlur;
                }
                else if (VortexEffect == StdBlur && !b)
                {
                    VortexEffect = null;
                    MO2Effect = StdBlur;
                }
            }
        }
    }
}
