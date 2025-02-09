using System.Windows;
using System.Reactive.Disposables;
using ReactiveUI;
using System.Reactive.Linq;

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
            this.OneWayBindStrict(this.ViewModel, x => x.LoginVM, x => x.LoginView.ViewModel)
                .DisposeWith(disposable);
            this.OneWayBindStrict(this.ViewModel, x => x.PerformanceVM, x => x.PerformanceView.ViewModel)
                .DisposeWith(disposable);
            this.OneWayBindStrict(this.ViewModel, x => x.AboutVM, x => x.AboutView.ViewModel)
                .DisposeWith(disposable);

            ViewModel.WhenAnyValue(vm => vm.ApiToken)
                     .Select(token => !string.IsNullOrEmpty(token?.AuthorKey) ? Visibility.Visible : Visibility.Collapsed)
                     .BindToStrict(this, v => v.FileUploadSettingsView.Visibility)
                     .DisposeWith(disposable);

            this.FileUploadSettingsView.ViewModel = this.ViewModel;
            this.MiscGalleryView.ViewModel = this.ViewModel;
        });
    }
}
