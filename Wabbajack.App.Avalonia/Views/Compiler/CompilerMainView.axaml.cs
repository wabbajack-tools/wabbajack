using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.Views.Compiler;

public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    private bool _clickedPublish;
    private bool _clickedPublishCollection;

    public CompilerMainView()
    {
        InitializeComponent();

        // WPF bound the image's height through MathConverter: 16:9 at its own width, rounded as its layout was.
        DetailImage.GetObservable(BoundsProperty)
            .Subscribe(b => DetailImage.Height = Math.Round(b.Width / (16.0 / 9.0)));

        // WPF bound these in XAML. Here a binding on a ReactiveUserControl's ViewModel would resolve against the
        // DataContext that same ViewModel replaces, so they are handed over as the screen's view model arrives,
        // before either is shown. Avalonia only carries a ViewModel over to the DataContext when the two already
        // matched, and a child inherits this screen's, so both are set. The CPU view follows the DataContext,
        // which is this screen's view model.
        this.WhenAnyValue(v => v.ViewModel)
            .Subscribe(vm =>
            {
                CompilerDetailsView.DataContext = CompilerDetailsView.ViewModel = vm?.CompilerDetailsVM;
                FileManager.DataContext = FileManager.ViewModel = vm?.CompilerFileManagerVM;
            });

        this.WhenActivated(disposables =>
        {
            _clickedPublish = false;
            _clickedPublishCollection = false;

            ViewModel!.WhenAnyValue(vm => vm.Settings.ModListImage)
                .Where(i => i.FileExists())
                .Subscribe(i =>
                {
                    try
                    {
                        var image = new Bitmap(i.ToString());
                        CompiledImage.Image = DetailImage.Image = image;
                    }
                    catch (Exception)
                    {
                        // WPF's TryGetBitmapImageFromFile: an image that will not load leaves the last one showing.
                    }
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.Settings.ModListName)
                .Subscribe(name => DetailImage.Title = CompiledImage.Title = name)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.Settings.ModListAuthor)
                .Subscribe(author => DetailImage.Author = CompiledImage.Author = author)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.State)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(s =>
                {
                    SetHidden(CompilerDetailsView, s != CompilerState.Configuration);
                    SetHidden(FileManager, s != CompilerState.Configuration);
                    SetHidden(ConfigurationButtons, s != CompilerState.Configuration);
                    SetHidden(LogView, s is not (CompilerState.Compiling or CompilerState.Errored));
                    SetHidden(CpuView, s != CompilerState.Compiling);
                    SetHidden(CompilationButtons, s != CompilerState.Compiling);
                    OpenFolderButton.IsEnabled = s == CompilerState.Completed;
                    CompiledImage.IsVisible = true;
                    SetHidden(CompiledImage, s != CompilerState.Completed);
                    SetHidden(CompletedButtons, s != CompilerState.Completed);
                    PreflightStatusText.IsVisible = s == CompilerState.Completed;
                })
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.StartCommand, x => x.StartButton)
                .DisposeWith(disposables);

            var startCommand = ViewModel!.StartCommand;
            Observable.FromEventPattern<EventHandler, EventArgs>(
                    h => startCommand.CanExecuteChanged += h, h => startCommand.CanExecuteChanged -= h)
                .Select(_ => startCommand.CanExecute(null))
                .StartWith(startCommand.CanExecute(null))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(canStart => ToolTip.SetTip(StartButton,
                    canStart ? null : "Cannot start compilation, not all required fields have been filled out."))
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
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(passed => RefreshPreflightButton.IsVisible = passed == false)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PreflightCheckMessage)
                .Where(msg => !string.IsNullOrWhiteSpace(msg))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(msg => PreflightStatusText.Text = msg)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.PublishingPercentage, vm => vm.PreflightChecksPassed, vm => vm.PublishLastResult)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    var (pct, preflightPassed, lastResult) = x;

                    if (pct != Percent.One) _clickedPublish = true;

                    PublishButton.ProgressPercentage = pct;

                    if (preflightPassed == false)
                        PublishButton.Text = "Checks Failed - Cannot Publish";
                    else if (pct.Value >= 0 && pct.Value < 1)
                        PublishButton.Text = "Publishing...";
                    else if (lastResult == CompilerMainVM.PublishResult.Failed)
                        PublishButton.Text = "Publish Failed";
                    else
                        PublishButton.Text = _clickedPublish ? "Publish Completed" : "Publish Modlist";
                })
                .DisposeWith(disposables);

            PublishCollectionButton.Click += OnPublishCollectionClick;
            Disposable.Create(() => PublishCollectionButton.Click -= OnPublishCollectionClick).DisposeWith(disposables);

            ViewModel.WhenAnyValue(
                    vm => vm.CollectionPublishingPercentage,
                    vm => vm.CollectionPublishingStage,
                    vm => vm.IsPublishingCollection,
                    vm => vm.PublishCollectionLastResult,
                    vm => vm.PreflightChecksPassed,
                    vm => vm.ExistingCollectionRevisionNumber,
                    vm => vm.IsCheckingCollectionStatus)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    var (percentage, stage, isBusy, result, preflightPassed, existingRevision, isChecking) = x;
                    var isDraft = ViewModel.ExistingCollectionIsDraft;

                    if (isBusy) _clickedPublishCollection = true;

                    PublishCollectionButton.ProgressPercentage = percentage;

                    if (preflightPassed == false)
                    {
                        PublishCollectionButton.Text = "Checks Failed - Cannot Publish";
                        PublishCollectionButton.IsEnabled = false;
                        return;
                    }

                    PublishCollectionButton.IsEnabled = !isBusy &&
                                                        !ViewModel.IsPublishing &&
                                                        ViewModel.State == CompilerState.Completed &&
                                                        preflightPassed == true;

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

                    if (!_clickedPublishCollection)
                    {
                        if (existingRevision.HasValue)
                            PublishCollectionButton.Text = isDraft
                                ? "Update Draft Revision on Nexus Mods"
                                : $"Push Revision {existingRevision.Value + 1} to Nexus Mods";
                        else
                            PublishCollectionButton.Text = "Create Nexus Mods Collection page";
                        return;
                    }

                    PublishCollectionButton.Text = result == CompilerMainVM.PublishCollectionResult.Success
                        ? existingRevision.HasValue
                            ? isDraft ? "Draft Updated Successfully" : "Revision Pushed Successfully"
                            : "Collection Created Successfully"
                        : "Collection Failed";
                })
                .DisposeWith(disposables);

            // IsBusy raises nothing of its own, so it is read off the two flags it is made of.
            ViewModel.WhenAnyValue(vm => vm.IsPublishing, vm => vm.IsPublishingCollection, (a, b) => a || b)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(isBusy =>
                {
                    MainContent.IsEnabled = !isBusy;
                    BusyOverlay.IsVisible = isBusy;
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.BusyStatusText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => BusyOverlayText.Text = t)
                .DisposeWith(disposables);
        });
    }

    /// <summary>
    ///     WPF's Visibility.Hidden: not drawn and not clickable, but still taking its room in the layout.
    /// </summary>
    private static void SetHidden(Control control, bool hidden)
    {
        control.Opacity = hidden ? 0 : 1;
        // Out of hit testing, so the pointer and the wheel reach what is under it, and disabled so nothing in it
        // takes keyboard focus either; nothing in a hidden part shows, so its disabled look does not matter.
        control.IsHitTestVisible = !hidden;
        control.IsEnabled = !hidden;
    }

    private void OnPublishCollectionClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || !((System.Windows.Input.ICommand)vm.PublishCollectionCommand).CanExecute(null))
            return;

        var yes = NativeMessageBox.AskYesNo(
            "Publishing to Nexus Mods will create a collection page that allows users browsing Nexus Mods to discover your Wabbajack list.\n\n" +
            "Important Notes:\n" +
            "• This will NOT create a Vortex collection\n" +
            "• The first publish creates a mostly blank collection page ( with the list of mods prefilled), which you can edit manually\n" +
            "• Subsequent publishes create new revisions of that same collection\n" +
            "• Users can initiate the Wabbajack download directly from the Nexus Mods webpage\n\n" +
            "Do you want to continue?",
            "Publish Nexus Mods Collection");

        if (yes) ((System.Windows.Input.ICommand)vm.PublishCollectionCommand).Execute(null);
    }
}
