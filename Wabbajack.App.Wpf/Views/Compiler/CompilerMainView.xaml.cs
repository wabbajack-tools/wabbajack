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
using System.Threading.Tasks;
using static System.Windows.Visibility;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilingView.xaml
/// </summary>
public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    private bool _ClickedPublish = false;
    private bool _ClickedPublishCollection = false;

    public CompilerMainView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            _ClickedPublish = false;
            _ClickedPublishCollection = false;

            ViewModel.WhenAny(vm => vm.Settings.ModListImage)
                .Where(i => i.FileExists())
                .Select(i => (UIUtils.TryGetBitmapImageFromFile(i, out var img), img))
                .Subscribe(x =>
                {
                    bool success = x.Item1;
                    if (success)
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
                .Select(s => s == CompilerState.Configuration ? Visible : Hidden)
                .BindToStrict(this, view => view.CompilerDetailsView.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Configuration ? Visible : Hidden)
                .BindToStrict(this, view => view.FileManager.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Configuration ? Visible : Hidden)
                .BindToStrict(this, view => view.ConfigurationButtons.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Compiling || s == CompilerState.Errored ? Visible : Hidden)
                .BindToStrict(this, x => x.LogView.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Compiling ? Visible : Hidden)
                .BindToStrict(this, x => x.CpuView.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Compiling ? Visible : Hidden)
                .BindToStrict(this, view => view.CompilationButtons.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Completed)
                .BindToStrict(this, view => view.OpenFolderButton.IsEnabled)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Completed ? Visible : Hidden)
                .BindToStrict(this, view => view.CompiledImage.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                .Select(s => s == CompilerState.Completed ? Visible : Hidden)
                .BindToStrict(this, view => view.CompletedButtons.Visibility)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.StartCommand, x => x.StartButton)
                .DisposeWith(disposables);

            ViewModel.StartCommand.Events().CanExecuteChanged
                .Subscribe(_ =>
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

            this.BindCommand(ViewModel, x => x.RefreshPreflightChecksCommand, x => x.RefreshPreflightButton)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PreflightChecksPassed)
                .Select(passed => passed == false ? Visible : Collapsed)
                .BindToStrict(this, v => v.RefreshPreflightButton.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PreflightCheckMessage)
                .Where(msg => !string.IsNullOrWhiteSpace(msg))
                .BindToStrict(this, v => v.PreflightStatusText.Text)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.State)
                .Select(s => s == CompilerState.Completed ? Visible : Collapsed)
                .BindToStrict(this, v => v.PreflightStatusText.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PublishingPercentage, vm => vm.PreflightChecksPassed)
                .ObserveOnGuiThread()
                .Subscribe(x =>
                {
                    var pct = x.Item1;
                    var preflightPassed = x.Item2;

                    if (pct != RateLimiter.Percent.One) _ClickedPublish = true;

                    PublishButton.ProgressPercentage = pct;

                    if (preflightPassed == false)
                    {
                        PublishButton.Text = "Checks Failed - Cannot Publish";
                    }
                    else if (pct.Value >= 0 && pct.Value < 1)
                    {
                        PublishButton.Text = "Publishing...";
                    }
                    else
                    {
                        PublishButton.Text = _ClickedPublish ? "Publish Completed" : "Publish Modlist";
                    }
                })
                .DisposeWith(disposables);




            PublishCollectionButton.Events().Click
                .Subscribe(async _ => await HandlePublishCollectionClick())
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(
                    vm => vm.CollectionPublishingPercentage,
                    vm => vm.CollectionPublishingStage,
                    vm => vm.IsPublishingCollection,
                    vm => vm.PublishCollectionLastResult,
                    vm => vm.PreflightChecksPassed,
                    vm => vm.ExistingCollectionRevisionNumber,
                    vm => vm.IsCheckingCollectionStatus)
                .ObserveOnGuiThread()
                .Subscribe(x =>
                {
                    var percentage = x.Item1;
                    var stage = x.Item2;
                    var isBusy = x.Item3;
                    var result = x.Item4;
                    var preflightPassed = x.Item5;
                    var existingRevision = x.Item6;
                    var isChecking = x.Item7;

                    if (isBusy) _ClickedPublishCollection = true;

                    // Update progress bar
                    PublishCollectionButton.ProgressPercentage = percentage;

                    if (preflightPassed == false)
                    {
                        PublishCollectionButton.Text = "Checks Failed - Cannot Publish";
                        PublishCollectionButton.IsEnabled = false;
                        return;
                    }

                    // Update enabled state based on all conditions
                    var canExecute = !isBusy &&
                                    !ViewModel.IsPublishing &&
                                    ViewModel.State == CompilerState.Completed &&
                                    preflightPassed == true;

                    PublishCollectionButton.IsEnabled = canExecute;

                    if (isBusy)
                    {
                        PublishCollectionButton.Text = stage;
                        return;
                    }

                    if (isChecking)
                    {
                        PublishCollectionButton.Text = "Checking collection status...";
                        return;
                    }

                    if (!_ClickedPublishCollection)
                    {
                        if (existingRevision.HasValue)
                        {
                            PublishCollectionButton.Text = $"Push Revision {existingRevision.Value + 1} to Nexus Mods";
                        }
                        else
                        {
                            PublishCollectionButton.Text = "Create Nexus Mods Collection page";
                        }
                        return;
                    }

                    PublishCollectionButton.Text = result == CompilerMainVM.PublishCollectionResult.Success
                        ? (existingRevision.HasValue ? "Revision Pushed Successfully" : "Collection Created Successfully")
                        : "Collection Failed";
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.IsBusy)
                .Select(isBusy => !isBusy)
                .BindToStrict(this, v => v.MainContent.IsEnabled)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.IsBusy)
                .Select(isBusy => isBusy ? Visible : Collapsed)
                .BindToStrict(this, v => v.BusyOverlay.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.BusyStatusText)
                .BindToStrict(this, v => v.BusyOverlayText.Text)
                .DisposeWith(disposables);
        });
    }
    private async Task HandlePublishCollectionClick()
    {
        if (!ViewModel.PublishCollectionCommand.CanExecute(null))
            return;

        var result = MessageBox.Show(
            "Publishing to Nexus Mods will create a collection page that allows users browsing Nexus Mods to discover your Wabbajack list.\n\n" +
            "Important Notes:\n" +
            "• This will NOT create a Vortex collection\n" +
            "• The first publish creates a mostly blank collection page ( with the list of mods prefilled), which you can edit manually\n" +
            "• Subsequent publishes create new revisions of that same collection\n" +
            "• Users can initiate the Wabbajack download directly from the Nexus Mods webpage\n\n" +
            "Do you want to continue?",
            "Publish Nexus Mods Collection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            ViewModel.PublishCollectionCommand.Execute(null);
        }
    }
}