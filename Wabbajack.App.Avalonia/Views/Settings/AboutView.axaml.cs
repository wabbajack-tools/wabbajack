using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class AboutView : ReactiveUserControl<AboutVM>
{
    public AboutView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });

        // The logo is a fixed share of the card's width, as WPF's MathConverter "x/2.25" had it.
        AboutGrid.SizeChanged += (_, e) => Logo.Width = e.NewSize.Width / 2.25;
    }
}
