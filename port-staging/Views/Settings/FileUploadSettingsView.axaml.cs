using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for FileUploadSettingsView.axaml
/// </summary>
public partial class FileUploadSettingsView : ReactiveUserControl<SettingsVM>
{
    public FileUploadSettingsView()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            this.WhenAnyValue(x => x.ViewModel.OpenFileUploadCommand)
                .BindToStrict(this, x => x.OpenFileUploadButton.Command)
                .DisposeWith(disposable);

            this.WhenAnyValue(x => x.ViewModel.BrowseUploadsCommand)
                .BindToStrict(this, x => x.BrowseUploadedFilesButton.Command)
                .DisposeWith(disposable);

            // WPF bound Visibility (Visible/Collapsed) via a Select over the token ->
            // Avalonia binds IsVisible (bool) directly, so the Visibility enum Select is dropped.
            ViewModel.WhenAnyValue(vm => vm.ApiToken.AuthorKey)
                     .Select(token => !string.IsNullOrEmpty(token))
                     .BindToStrict(this, v => v.OpenFileUploadButton.IsVisible)
                     .DisposeWith(disposable);
        });
    }
}
