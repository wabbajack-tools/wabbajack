using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using ReactiveUI;
using Wabbajack.Preflight;

namespace Wabbajack.Views.Preflight;

public partial class PreflightView : ReactiveUserControl<PreflightViewModel>
{
    public PreflightView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            // Header bindings
            this.OneWayBind(ViewModel, vm => vm.ModlistName, v => v.ModlistNameText.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ViewReadmeCommand, v => v.ViewReadmeButton)
                .DisposeWith(disposables);

            // Path pickers — bind to the shared FilePickerVM instances
            this.Bind(ViewModel, vm => vm.InstallLocation, v => v.InstallLocationPicker.PickerVM)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DownloadLocation, v => v.DownloadLocationPicker.PickerVM)
                .DisposeWith(disposables);

            // Failed checks list
            this.OneWayBind(ViewModel, vm => vm.FailedChecks, v => v.FailedChecksList.ItemsSource)
                .DisposeWith(disposables);

            // Install button
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.InstallButton)
                .DisposeWith(disposables);

            // Summary bar: update colors and text reactively
            this.WhenAnyValue(v => v.ViewModel.AllPassed, v => v.ViewModel.PassedCount, v => v.ViewModel.TotalCount)
                .ObserveOnGuiThread()
                .Subscribe(tuple =>
                {
                    var (allPassed, passed, total) = tuple;
                    if (allPassed)
                    {
                        SummaryBar.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x2a, 0x1a));
                        SummaryText.Foreground = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
                        SummaryText.Text = $"\u2713 All {total} checks passed \u2014 Ready to install";
                    }
                    else
                    {
                        SummaryBar.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x2a, 0x1a));
                        SummaryText.Foreground = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
                        SummaryText.Text = $"\u2713 {passed} of {total} checks passed";
                    }
                })
                .DisposeWith(disposables);

            // Enable/disable install button based on AllPassed
            this.WhenAnyValue(v => v.ViewModel.AllPassed)
                .ObserveOnGuiThread()
                .Subscribe(allPassed => InstallButton.IsEnabled = allPassed)
                .DisposeWith(disposables);
        });
    }
}
