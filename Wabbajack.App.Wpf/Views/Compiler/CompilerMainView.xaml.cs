using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using System.Windows;
using System.Reactive.Disposables;
using System;
using ReactiveMarbles.ObservableEvents;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilingView.xaml
/// </summary>
public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    private bool _ClickedPublish = false;

    public CompilerMainView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            _ClickedPublish = false;
            ViewModel.WhenAny(vm => vm.Settings.ModListImage)
                .Where(i => i.FileExists())
                .Select(i => (UIUtils.TryGetBitmapImageFromFile(i, out var img), img))
                .Subscribe(x =>
                {
                    bool success = x.Item1;

                    if(success)
                    {
                        CompiledImage.Image = DetailImage.Image = x.img;
                    }
                })
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListName)
                .BindToStrict(this, view => view.DetailImage.Title)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListAuthor)
                .BindToStrict(this, view => view.DetailImage.Author)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListName)
                .BindToStrict(this, view => view.CompiledImage.Title)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListAuthor)
                .BindToStrict(this, view => view.CompiledImage.Author)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                     .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, view => view.CompilerDetailsView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                     .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, view => view.FileManager.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ConfigurationButtons.Visibility)
                    .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                     .Select(s => s == CompilerState.Compiling || s == CompilerState.Errored ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, x => x.LogView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                     .Select(s => s == CompilerState.Compiling ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, x => x.CpuView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Compiling ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.CompilationButtons.Visibility)
                    .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Completed)
                    .BindToStrict(this, view => view.OpenFolderButton.IsEnabled)
                    .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                     .Select(s => s == CompilerState.Completed ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, view => view.CompiledImage.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Completed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.CompletedButtons.Visibility)
                    .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.StartCommand, x => x.StartButton)
                .DisposeWith(disposables);

            ViewModel.StartCommand.Events().CanExecuteChanged
                .Subscribe(x =>
                {
                    if (!ViewModel.StartCommand.CanExecute(null))
                    {
                        StartButton.ToolTip = $"Cannot start compilation, not all required fields have been filled out.";
                    }
                    else StartButton.ToolTip = null;
                })
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.CancelCommand, x => x.CancelButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenLogCommand, x => x.OpenLogButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenFolderCommand, x => x.OpenFolderButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.PublishCommand, x => x.PublishButton)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PublishingPercentage)
                .ObserveOnGuiThread()
                .Subscribe(pct =>
                {
                    if (pct != RateLimiter.Percent.One) _ClickedPublish = true;
                    PublishButton.ProgressPercentage = pct;
                    PublishButton.Text = (pct.Value >= 0 && pct.Value < 1) ? "Publishing..." : _ClickedPublish ? "Publish Completed" : "Publish Modlist";
                })
                .DisposeWith(disposables);

        });
    }
}
