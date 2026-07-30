using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
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
            this.OneWayBindStrict(this.ViewModel, x => x.LoginVM, x => x.LoginView.ViewModel)
                .DisposeWith(disposable);
            this.OneWayBindStrict(this.ViewModel, x => x.PerformanceVM, x => x.PerformanceView.ViewModel)
                .DisposeWith(disposable);
            this.OneWayBindStrict(this.ViewModel, x => x.AboutVM, x => x.AboutView.ViewModel)
                .DisposeWith(disposable);

            // WPF bound Visibility (Visible/Collapsed) via a Select over the token ->
            // Avalonia binds IsVisible (bool) directly, so the Visibility enum Select is dropped.
            ViewModel.WhenAnyValue(vm => vm.ApiToken)
                     .Select(token => !string.IsNullOrEmpty(token?.AuthorKey))
                     .BindToStrict(this, v => v.FileUploadSettingsView.IsVisible)
                     .DisposeWith(disposable);

            this.FileUploadSettingsView.ViewModel = this.ViewModel;
            this.MiscGalleryView.ViewModel = this.ViewModel;
        });
    }
}
