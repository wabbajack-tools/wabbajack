using System.Windows;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for FileUploadSettingsView.xaml
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

            ViewModel.WhenAnyValue(vm => vm.ApiToken.AuthorKey)
                     .Select(token => !string.IsNullOrEmpty(token) ? Visibility.Visible : Visibility.Collapsed)
                     .BindToStrict(this, v => v.OpenFileUploadButton.Visibility)
                     .DisposeWith(disposable);
        });
    }
}
