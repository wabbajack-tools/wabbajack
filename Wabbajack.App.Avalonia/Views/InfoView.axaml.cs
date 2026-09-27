using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class InfoView : ReactiveUserControl<InfoVM>
{
    public InfoView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.BindCommand(ViewModel, x => x.CloseCommand, x => x.PrevButton)
                .DisposeWith(dispose);
        });
    }
}
