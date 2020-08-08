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
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for MiscSettingsView.xaml
    /// </summary>
    public partial class MiscSettingsView : ReactiveUserControl<SettingsVM>
    {
        public MiscSettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                // Bind Values
                this.BindStrict(this.ViewModel, x => x.Filters.IsPersistent, x => x.FilterPersistCheckBox.IsChecked)
                    .DisposeWith(disposable);
                this.BindStrict(this.ViewModel, x => x.Filters.UseCompression, x => x.UseCompressionCheckBox.IsChecked)
                    .DisposeWith(disposable);
                this.WhenAnyValue(x => x.ViewModel.OpenTerminalCommand)
                    .BindToStrict(this, x => x.OpenTerminal.Command)
                    .DisposeWith(disposable);

                this.ClearCefCache.Click += (sender, args) => {Driver.ClearCache();};
            });
        }
    }
}
