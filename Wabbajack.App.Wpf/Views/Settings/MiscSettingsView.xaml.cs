using System.Reactive.Disposables;
using ReactiveUI;

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
            });
        }
    }
}
