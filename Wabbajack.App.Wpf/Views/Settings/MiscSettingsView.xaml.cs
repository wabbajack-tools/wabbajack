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
                .BindToStrict(this, x => x.OpenTerminalButton.Command)
                .DisposeWith(disposable);

            this.WhenAnyValue(x => x.ViewModel.ResetCommand)
                .BindToStrict(this, x => x.ResetButton.Command)
                .DisposeWith(disposable);
        });
    }
}
