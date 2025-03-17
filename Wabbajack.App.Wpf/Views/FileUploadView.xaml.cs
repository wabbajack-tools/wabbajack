using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveMarbles.ObservableEvents;
using System.Windows.Input;
using System.Windows;
using System.IO;
using Wabbajack.Paths;

namespace Wabbajack;

public partial class FileUploadView : ReactiveUserControl<FileUploadVM>
{
    public FileUploadView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BrowseUploadsCommand, v => v.BrowseUploadsButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.UploadMoreFilesCommand, v => v.UploadMoreFilesButton)
                .DisposeWith(disposables);

            UploadBackground.Events().DragEnter
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragEnter())
                .DisposeWith(disposables);

            StartUploadIcon.Events().DragEnter
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragEnter())
                .DisposeWith(disposables);

            DragToUploadText.Events().DragEnter
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragEnter())
                .DisposeWith(disposables);

            UploadBackground.Events().DragLeave
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragLeave())
                .DisposeWith(disposables);

            StartUploadIcon.Events().DragLeave
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragLeave())
                .DisposeWith(disposables);

            DragToUploadText.Events().DragLeave
                .ObserveOnGuiThread()
                .Subscribe(_ => OnDragLeave())
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.UploadProgress)
                     .ObserveOnGuiThread()
                     .Select(up => $"{Math.Round(up * 100)}%")
                     .Subscribe(up => ProgressText.Text = up)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.UploadProgress)
                     .ObserveOnGuiThread()
                     .Subscribe(progress =>
                     {
                         UploadBackground.AllowDrop = progress <= 0;

                         if (progress <= 0)
                         {
                             StartUploadGrid.Visibility = Visibility.Visible;
                             UploadingGrid.Visibility = Visibility.Collapsed;
                             UploadCompletedGrid.Visibility = Visibility.Collapsed;
                         }
                         else if (progress > 0 && progress < 1)
                         {
                             StartUploadGrid.Visibility = Visibility.Collapsed;
                             UploadingGrid.Visibility = Visibility.Visible;
                             UploadCompletedGrid.Visibility = Visibility.Collapsed;
                         }
                         else if (progress >= 1)
                         {
                             StartUploadGrid.Visibility = Visibility.Collapsed;
                             UploadingGrid.Visibility = Visibility.Collapsed;
                             UploadCompletedGrid.Visibility = Visibility.Visible;
                         }
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.FileUrl)
                     .BindToStrict(this, v => v.FileUrlText.Text)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.CopyUrlCommand)
                     .BindToStrict(this, v => v.FileUrlHyperlink.Command)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.BrowseAndUploadFileCommand)
                     .BindToStrict(this, v => v.ChooseFileHyperlink.Command)
                     .DisposeWith(disposables);
        });
    }

    private void OnDragEnter()
    {
        StartUploadIcon.IconVariant = FluentIcons.Common.IconVariant.Filled;
        UploadBackground.Fill = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundBrush"];
    }

    private void OnDragLeave()
    {
        StartUploadIcon.IconVariant = FluentIcons.Common.IconVariant.Regular;
        UploadBackground.Fill = (System.Windows.Media.Brush)Application.Current.Resources["ComplementaryPrimary08Brush"];
    }

    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        OnDragLeave();

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        ViewModel.UploadProgress = 0;

        var filePath = (AbsolutePath)((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        ViewModel.Picker.TargetPath = filePath;
        ViewModel.UploadCommand.Execute(null);

    }
}

