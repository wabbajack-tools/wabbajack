using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsVM>
{
    public SettingsView()
    {
        InitializeComponent();
        // Activating the view is what activates its view model, as in WPF.
        this.WhenActivated(_ => { });
    }
}
