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

            this.AutoButton.Command = ReactiveCommand.Create(
                execute: () => this.ViewModel.Manual = false,
                canExecute: this.WhenAny(x => x.ViewModel.Manual)
                    .ObserveOnGuiThread());
            this.ManualButton.Command = ReactiveCommand.Create(
                execute: () => this.ViewModel.Manual = true,
                canExecute: this.WhenAny(x => x.ViewModel.Manual)
                    .Select(x => !x)
                    .ObserveOnGuiThread());

            this.WhenActivated(disposable =>
            {
                // Bind mode buttons

                // Modify visibility of controls based on if auto
                this.OneWayBindStrict(this.ViewModel, x => x.Manual, x => x.MaxCoresLabel.Visibility,
                        b => b ? Visibility.Visible : Visibility.Collapsed)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Manual, x => x.MaxCoresSpinner.Visibility,
                        b => b ? Visibility.Visible : Visibility.Collapsed)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Manual, x => x.TargetUsageLabel.Visibility,
                        b => b ? Visibility.Collapsed : Visibility.Visible)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Manual, x => x.TargetUsageSpinner.Visibility,
                        b => b ? Visibility.Collapsed : Visibility.Visible)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Manual, x => x.TargetUsageSlider.Visibility,
                        b => b ? Visibility.Collapsed : Visibility.Visible)
                    .DisposeWith(disposable);

                // Bind Values
                this.BindStrict(this.ViewModel, x => x.MaxCores, x => x.MaxCoresSpinner.Value,
                        vmToViewConverter: x => x,
                        viewToVmConverter: x => (byte)(x ?? 0))
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.TargetUsage, x => x.TargetUsageSpinner.Value)
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.TargetUsage, x => x.TargetUsageSlider.Value)
                    .DisposeWith(disposable);
            });
        }
    }
}
