using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class PerformanceSettingsView : ReactiveUserControl<PerformanceSettingsVM>
{
    public PerformanceSettingsView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }
}
