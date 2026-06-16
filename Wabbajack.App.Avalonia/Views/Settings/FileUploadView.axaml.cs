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

        // Gate the three sections on UploadProgress (0..1), matching the original WPF view.
        // (We can't gate on FileUrl: the VM overwrites it with human-readable status text on
        // every progress message, so it's non-empty for most of the upload — only the final
        // message is the real URL, which COMPLETED shows once progress reaches 1.)
        //   START      = UploadProgress <= 0
        //   UPLOADING  = 0 < UploadProgress < 1
        //   COMPLETED  = UploadProgress >= 1
        this.WhenActivated(dispose =>
        {
            var progress = this.WhenAnyValue(x => x.ViewModel!.UploadProgress);

            progress.Select(p => p <= 0)
                .BindTo(startSection, x => x.IsVisible)
                .DisposeWith(dispose);

            progress.Select(p => p > 0 && p < 1)
                .BindTo(uploadingSection, x => x.IsVisible)
                .DisposeWith(dispose);

            progress.Select(p => p >= 1)
                .BindTo(completedSection, x => x.IsVisible)
                .DisposeWith(dispose);
        });
    }
}
