using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;
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

            // NOTE: WPF wired these via ReactiveMarbles' .Events() (UploadBackground/StartUploadIcon/
            // DragToUploadText each raise DragEnter/DragLeave since they overlap in the visual tree and
            // the drag can be hit-tested over any of them). Avalonia controls have no CLR DragEnter/
            // DragLeave events for .Events() to bind to, so these are wired manually via
            // AddHandler/RemoveHandler against the Avalonia.Input.DragDrop routed events instead.
            SubscribeDragEnter(UploadBackground, disposables);
            SubscribeDragEnter(StartUploadIcon, disposables);
            SubscribeDragEnter(DragToUploadText, disposables);

            SubscribeDragLeave(UploadBackground, disposables);
            SubscribeDragLeave(StartUploadIcon, disposables);
            SubscribeDragLeave(DragToUploadText, disposables);

            ViewModel.WhenAnyValue(vm => vm.UploadProgress)
                     .ObserveOnGuiThread()
                     .Select(up => $"{Math.Round(up * 100)}%")
                     .Subscribe(up => ProgressText.Text = up)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.UploadProgress)
                     .ObserveOnGuiThread()
                     .Subscribe(progress =>
                     {
                         DragDrop.SetAllowDrop(UploadBackground, progress <= 0);

                         if (progress <= 0)
                         {
                             StartUploadGrid.IsVisible = true;
                             UploadingGrid.IsVisible = false;
                             UploadCompletedGrid.IsVisible = false;
                         }
                         else if (progress > 0 && progress < 1)
                         {
                             StartUploadGrid.IsVisible = false;
                             UploadingGrid.IsVisible = true;
                             UploadCompletedGrid.IsVisible = false;
                         }
                         else if (progress >= 1)
                         {
                             StartUploadGrid.IsVisible = false;
                             UploadingGrid.IsVisible = false;
                             UploadCompletedGrid.IsVisible = true;
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

    private void SubscribeDragEnter(Control control, CompositeDisposable disposables)
    {
        void Handler(object sender, DragEventArgs e) => OnDragEnter();
        control.AddHandler(DragDrop.DragEnterEvent, Handler);
        Disposable.Create(() => control.RemoveHandler(DragDrop.DragEnterEvent, Handler)).DisposeWith(disposables);
    }

    private void SubscribeDragLeave(Control control, CompositeDisposable disposables)
    {
        void Handler(object sender, DragEventArgs e) => OnDragLeave();
        control.AddHandler(DragDrop.DragLeaveEvent, Handler);
        Disposable.Create(() => control.RemoveHandler(DragDrop.DragLeaveEvent, Handler)).DisposeWith(disposables);
    }

    private void OnDragEnter()
    {
        StartUploadIcon.IconVariant = FluentIcons.Common.IconVariant.Filled;
        if (Application.Current!.TryFindResource("BackgroundBrush", out var brush))
        {
            UploadBackground.Fill = (IBrush)brush!;
        }
    }

    private void OnDragLeave()
    {
        StartUploadIcon.IconVariant = FluentIcons.Common.IconVariant.Regular;
        if (Application.Current!.TryFindResource("ComplementaryPrimary08Brush", out var brush))
        {
            UploadBackground.Fill = (IBrush)brush!;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        OnDragLeave();

        // NOTE: WPF used DataFormats.FileDrop + IDataObject.GetData(string). Avalonia's drag/drop
        // surface (Avalonia.Input.IDataObject) instead exposes DataFormats.Files + GetFiles(),
        // returning IStorageItem entries; the local file path comes from IStorageItem.Path.LocalPath.
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles()?.ToList();
        if (files is null || files.Count == 0) return;

        ViewModel.UploadProgress = 0;

        var filePath = (AbsolutePath)files[0].Path.LocalPath;
        ViewModel.Picker.TargetPath = filePath;
        ViewModel.UploadCommand.Execute(null);
    }
}
