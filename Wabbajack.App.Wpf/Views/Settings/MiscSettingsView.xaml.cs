using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for MiscSettingsView.xaml
/// </summary>
public partial class MiscSettingsView : ReactiveUserControl<SettingsVM>
{
    public MiscSettingsView()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            // Bind Values
            this.WhenAnyValue(x => x.ViewModel.OpenTerminalCommand)
                .BindToStrict(this, x => x.OpenTerminal.Command)
                .DisposeWith(disposable);
        });
    }
}
