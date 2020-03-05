using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                this.OneWayBindStrict(ViewModel, x => x.BackCommand, x => x.BackButton.Command)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(ViewModel, x => x.Login, x => x.LoginView.ViewModel)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(ViewModel, x => x.Performance, x => x.PerformanceView.ViewModel)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(ViewModel, x => x.SlideShowSettings, x => x.SlideShowSettingsView.ViewModel)
                    .DisposeWith(disposable);
            });
        }
    }
}
