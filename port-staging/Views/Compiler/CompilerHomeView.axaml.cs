using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.Common;
using ReactiveMarbles.ObservableEvents;
using System.Reactive;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilerHomeView.axaml
/// </summary>
public partial class CompilerHomeView : ReactiveUserControl<CompilerHomeVM>
{
    public CompilerHomeView()
    {
        InitializeComponent();

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.CompiledModLists)
                .BindToStrict(this, x => x.CompiledModListsControl.ItemsSource)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.NewModlistCommand)
                .BindToStrict(this, x => x.NewModlistButton.Command)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.LoadSettingsCommand)
                .BindToStrict(this, x => x.LoadSettingsButton.Command)
                .DisposeWith(dispose);
        });
    }
}
