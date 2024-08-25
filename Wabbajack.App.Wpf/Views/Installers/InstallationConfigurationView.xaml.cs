using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
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
    /// Interaction logic for InstallationConfigurationView.xaml
    /// </summary>
    public partial class InstallationConfigurationView : ReactiveUserControl<InstallerVM>
    {
        public InstallationConfigurationView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.Installer.ConfigVisualVerticalOffset)
                    .Select(i => (double)i)
                    .BindToStrict(this, x => x.InstallConfigSpacer.Height)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ModListLocation)
                    .BindToStrict(this, x => x.ModListLocationPicker.PickerVM)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Installer)
                    .BindToStrict(this, x => x.InstallerCustomizationContent.Content)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.BeginCommand)
                    .BindToStrict(this, x => x.BeginButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.VerifyCommand)
                    .BindToStrict(this, x => x.VerifyButton.Command)
                    .DisposeWith(dispose);
                this.BindStrict(ViewModel, vm => vm.OverwriteFiles, x => x.OverwriteCheckBox.IsChecked)
                    .DisposeWith(dispose);

                // Error handling

                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => !v.Failed)
                    .BindToStrict(this, view => view.BeginButton.IsEnabled)
                    .DisposeWith(dispose);
                
                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => !v.Failed)
                    .BindToStrict(this, view => view.VerifyButton.IsEnabled)
                    .DisposeWith(dispose);

                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => v.Reason)
                    .BindToStrict(this, view => view.errorTextBox.Text)
                    .DisposeWith(dispose);

                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => v.Failed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ErrorSummaryIcon.Visibility)
                    .DisposeWith(dispose);
                
                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => v.Failed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ErrorSummaryIconGlow.Visibility)
                    .DisposeWith(dispose);
                
                this.WhenAnyValue(x => x.ViewModel.ErrorState)
                    .Select(v => v.Reason)
                    .BindToStrict(this, view => view.ErrorSummaryIcon.ToolTip)
                    .DisposeWith(dispose);
            });
        }
    }
}
