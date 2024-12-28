using System;
using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.Paths.IO;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for PerformanceSettingsView.xaml
    /// </summary>
    public partial class PerformanceSettingsView : ReactiveUserControl<PerformanceSettingsViewModel>
    {
        public PerformanceSettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                this.BindStrict(
                        ViewModel,
                        x => x.MaximumMemoryPerDownloadThreadMb,
                        x => x.MaximumMemoryPerDownloadThreadIntegerUpDown.Value)
                    .DisposeWith(disposable);

                this.BindStrict(
                        ViewModel,
                        x => x.MinimumFileSizeForResumableDownload,
                        x => x.MinimumFileSizeForResumableDownloadIntegerUpDown.Value)
                    .DisposeWith(disposable);

                this.EditResourceSettings.Command = ReactiveCommand.Create(() =>
                {
                    UIUtils.OpenFile(
                        KnownFolders.WabbajackAppLocal.Combine("saved_settings", "resource_settings.json"));
                    Environment.Exit(0);
                });

                ResetMaximumMemoryPerDownloadThread.Command = ReactiveCommand.Create(() =>
                {
                    ViewModel.ResetMaximumMemoryPerDownloadThreadMb();
                });

                ResetMinimumFileSizeForResumableDownload.Command = ReactiveCommand.Create(() =>
                {
                    ViewModel.ResetMinimumFileSizeForResumableDownload();
                });
            });
        }
    }
}
