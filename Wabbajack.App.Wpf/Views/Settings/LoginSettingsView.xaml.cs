using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for LoginSettingsView.xaml
/// </summary>
public partial class LoginSettingsView : ReactiveUserControl<LoginManagerVM>
{
    public LoginSettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            this.OneWayBindStrict(this.ViewModel, x => x.Logins, x => x.DownloadersList.ItemsSource)
                .DisposeWith(disposable);
        });
    }
}
