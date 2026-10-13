using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

public partial class BulkDownloadsView : ReactiveUserControl<BulkDownloadsVM>
{
    public BulkDownloadsView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Items)
                .Subscribe(items => RowsList.ItemsSource = items)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.FooterText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text => FooterText.Text = text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.IsDownloading)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(downloading => StopButton.IsVisible = downloading)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.StopDownloadsCommand, v => v.StopButton)
                .DisposeWith(dispose);
        });
    }
}
