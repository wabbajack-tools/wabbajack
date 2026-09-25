using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Converters;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Views;

public partial class FileUploadView : ReactiveUserControl<FileUploadVM>
{
    public FileUploadView()
    {
        InitializeComponent();

        // The drop area lights up while a file is dragged over it, the text on it included.
        foreach (var target in new InputElement[] { UploadBackground, StartUploadIcon, DragToUploadText })
        {
            target.AddHandler(DragDrop.DragEnterEvent, (_, _) => OnDragEnter());
            target.AddHandler(DragDrop.DragLeaveEvent, (_, _) => OnDragLeave());
            DragDrop.SetAllowDrop(target, true);
        }
        UploadBackground.AddHandler(DragDrop.DropEvent, OnDrop);

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.UploadMoreFilesCommand, v => v.UploadMoreFilesButton)
                .DisposeWith(disposables);

            ViewModel!.WhenAnyValue(vm => vm.BrowseUploadsCommand)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(cmd => BrowseUploadsButton.Command = cmd)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.UploadProgress)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(progress =>
                {
                    ProgressText.Text = $"{Math.Round(progress * 100)}%";
                    DragDrop.SetAllowDrop(UploadBackground, progress <= 0);
                    StartUploadGrid.IsVisible = progress <= 0;
                    UploadingGrid.IsVisible = progress > 0 && progress < 1;
                    UploadCompletedGrid.IsVisible = progress >= 1;
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.FileUrl)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(url => FileUrlText.Text = url)
                .DisposeWith(disposables);

            FileUrlHyperlink.Command = ViewModel!.CopyUrlCommand;
            ChooseFileHyperlink.Command = ViewModel.BrowseAndUploadFileCommand;
        });
    }

    private void OnDragEnter()
    {
        StartUploadIcon.IconVariant = IconVariant.Filled;
        UploadBackground.Fill = PreflightConverters.Brush("BackgroundBrush");
    }

    private void OnDragLeave()
    {
        StartUploadIcon.IconVariant = IconVariant.Regular;
        UploadBackground.Fill = PreflightConverters.Brush("ComplementaryPrimary08Brush");
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        OnDragLeave();

        var file = e.Data.GetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (file == null || ViewModel is not { } vm) return;

        vm.UploadProgress = 0;
        vm.Picker.TargetPath = (AbsolutePath)file;
        vm.UploadCommand.Execute(null);
    }
}
