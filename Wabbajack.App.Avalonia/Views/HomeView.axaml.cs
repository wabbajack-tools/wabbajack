using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class HomeView : ReactiveUserControl<HomeVM>
{
    public HomeView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.BrowseCommand, v => v.GetStartedButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Modlists)
                .Subscribe(modlists => ModlistCountText.Text = modlists?.Length.ToString() ?? "0")
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Modlists)
                .Subscribe(modlists => GameCountText.Text = modlists?.GroupBy(m => m.Game).Count().ToString() ?? "0")
                .DisposeWith(disposables);
        });
    }
}
