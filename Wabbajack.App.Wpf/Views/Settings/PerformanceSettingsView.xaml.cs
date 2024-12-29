using System;
using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.Paths.IO;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for PerformanceSettingsView.xaml
    /// </summary>
    public partial class PerformanceSettingsView : ReactiveUserControl<PerformanceSettings>
    {
        public PerformanceSettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {       
                this.EditResourceSettings.Command = ReactiveCommand.Create(() =>
                {
                    UIUtils.OpenFile(
                        KnownFolders.WabbajackAppLocal.Combine("saved_settings", "resource_settings.json"));
                    Environment.Exit(0);
                });
            });
        }
    }
}
