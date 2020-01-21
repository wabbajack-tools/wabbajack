using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using System.Windows.Controls.Primitives;
using System.Reactive.Linq;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CpuView.xaml
    /// </summary>
    public partial class CpuView : UserControlRx<ICpuStatusVM>
    {
        public double ProgressPercent
        {
            get => (double)GetValue(ProgressPercentProperty);
            set => SetValue(ProgressPercentProperty, value);
        }
        public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(double), typeof(CpuView),
             new FrameworkPropertyMetadata(default(double)));

        public MainSettings SettingsHook
        {
            get => (MainSettings)GetValue(SettingsHookProperty);
            set => SetValue(SettingsHookProperty, value);
        }
        public static readonly DependencyProperty SettingsHookProperty = DependencyProperty.Register(nameof(SettingsHook), typeof(MainSettings), typeof(CpuView),
             new FrameworkPropertyMetadata(default(SettingsVM), WireNotifyPropertyChanged));

        private bool _ShowingSettings;
        public bool ShowingSettings { get => _ShowingSettings; set => this.RaiseAndSetIfChanged(ref _ShowingSettings, value); }

        public CpuView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                Observable.CombineLatest(
                        this.WhenAny(x => x.ControlGrid.IsMouseOver),
                        this.WhenAny(x => x.SettingsHook.Performance.Manual)
                            .StartWith(true),
                        resultSelector: (over, manual) => over && !manual)
                    .Subscribe(showing =>
                    {
                        SettingsBar.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
                    })
                    .DisposeWith(disposable);

                this.OneWayBindStrict(this.ViewModel, x => x.StatusList, x => x.CpuListControl.ItemsSource)
                    .DisposeWith(disposable);

                this.BindStrict(this.ViewModel, x => x.MWVM.Settings.Performance.TargetUsage, x => x.TargetPercentageSlider.Value)
                    .DisposeWith(disposable);

                this.OneWayBindStrict(this.ViewModel, x => x.MWVM.Settings.Performance.TargetUsage, x => x.PercentageText.Text, x => $"{x.ToString("f2")}%")
                    .DisposeWith(disposable);

                this.WhenAny(x => x.ViewModel.CurrentCpuCount)
                    .DistinctUntilChanged()
                    .ObserveOnGuiThread()
                    .Subscribe(x =>
                    {
                        this.CpuCountText.Text = $"{x.CurrentCPUs} / {x.DesiredCPUs}";
                    })
                    .DisposeWith(disposable);
            });
        }
    }
}
