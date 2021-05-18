using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for PerformanceSettingsView.xaml
    /// </summary>
    public partial class PerformanceSettingsView : ReactiveUserControl<PerformanceSettings>
    {
        public PerformanceSettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                // Bind Values
                this.BindStrict(this.ViewModel, x => x.DiskThreads, x => x.DiskThreads.Value,
                        vmToViewConverter: x => x,
                        viewToVmConverter: x => (int)(x ?? 0))
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.DownloadThreads, x => x.DownloadThreads.Value,
                        vmToViewConverter: x => x,
                        viewToVmConverter: x => (int)(x ?? 0))
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.ReduceHDDThreads, x => x.ReduceHDDThreads.IsChecked)
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.FavorPerfOverRam, x => x.FavorPerfOverRam.IsChecked)
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.NetworkWorkaroundMode, x => x.UseNetworkWorkAround.IsChecked)
                    .DisposeWith(disposable);
            });
        }
    }
}
