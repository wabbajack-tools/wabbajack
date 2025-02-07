using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for SettingsView.xaml
/// </summary>
public partial class SettingsView : ReactiveUserControl<SettingsVM>
{
    public SettingsView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            this.OneWayBindStrict(this.ViewModel, x => x.Login, x => x.LoginView.ViewModel)
                .DisposeWith(disposable);
            this.OneWayBindStrict(this.ViewModel, x => x.Performance, x => x.PerformanceView.ViewModel)
                .DisposeWith(disposable);
            /*
            this.OneWayBindStrict(this.ViewModel, x => x.AuthorFile, x => x.AuthorFilesView.ViewModel)
                .DisposeWith(disposable);
            */
            this.MiscGalleryView.ViewModel = this.ViewModel;
        });
    }
}
