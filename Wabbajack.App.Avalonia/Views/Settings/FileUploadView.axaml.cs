using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

public partial class FileUploadView : ReactiveUserControl<FileUploadVM>
{
    public FileUploadView()
    {
        AvaloniaXamlLoader.Load(this);

        var startSection = this.FindControl<StackPanel>("StartSection")!;
        var uploadingSection = this.FindControl<StackPanel>("UploadingSection")!;
        var completedSection = this.FindControl<StackPanel>("CompletedSection")!;

        // The VM's IsUploading observable is private, so we gate the three sections
        // on the bindable UploadProgress (0..1) and FileUrl members:
        //   START      = FileUrl empty AND UploadProgress == 0
        //   UPLOADING  = FileUrl empty AND UploadProgress > 0
        //   COMPLETED  = FileUrl non-empty
        this.WhenActivated(dispose =>
        {
            var fileUrl = this.WhenAnyValue(x => x.ViewModel!.FileUrl);
            var progress = this.WhenAnyValue(x => x.ViewModel!.UploadProgress);

            fileUrl.CombineLatest(progress,
                    (url, p) => string.IsNullOrEmpty(url) && p == 0)
                .BindTo(startSection, x => x.IsVisible)
                .DisposeWith(dispose);

            fileUrl.CombineLatest(progress,
                    (url, p) => string.IsNullOrEmpty(url) && p > 0)
                .BindTo(uploadingSection, x => x.IsVisible)
                .DisposeWith(dispose);

            fileUrl.Select(url => !string.IsNullOrEmpty(url))
                .BindTo(completedSection, x => x.IsVisible)
                .DisposeWith(dispose);
        });
    }
}
