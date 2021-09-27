using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Material.Icons;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Controls
{
    public partial class BrowseItemView : ReactiveUserControl<BrowseItemViewModel>
    {
        public BrowseItemView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.Title, view => view.Title.Text)
                    .DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.Description, view => view.Description.Text)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.Image, view => view.ModListImage.Source)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.OpenWebsiteCommand, view => view.OpenWebsiteButton)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.State, view => view.ExecuteIcon.Kind, s => StateToKind(s));
                this.BindCommand(ViewModel, vm => vm.ExecuteCommand, view => view.ExecuteButton)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.Progress, view => view.DownloadProgressBar.Value,
                        s => s.Value * 1000)
                    .DisposeWith(disposables);
            });
        }

        private MaterialIconKind StateToKind(ModListState modListState)
        {
            return modListState switch
            {
                ModListState.Downloaded => MaterialIconKind.PlayArrow,
                ModListState.Downloading => MaterialIconKind.LocalAreaNetworkPending,
                ModListState.NotDownloaded => MaterialIconKind.Download,
                _ => throw new ArgumentOutOfRangeException(nameof(modListState), modListState, null)
            };
        }
    }
}