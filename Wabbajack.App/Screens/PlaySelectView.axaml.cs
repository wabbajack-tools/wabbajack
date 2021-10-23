using Avalonia.Controls.Mixins;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens;

public partial class PlaySelectView : ScreenBase<PlaySelectViewModel>
{
    public PlaySelectView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Items, view => view.Lists.Items)
                .DisposeWith(disposables);
        });
    }
}