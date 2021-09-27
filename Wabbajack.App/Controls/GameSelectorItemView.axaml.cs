using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack.App.Controls
{
    public partial class GameSelectorItemView : ReactiveUserControl<GameSelectorItemViewModel>
    {
        public GameSelectorItemView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.Name, view => view.GameName.Text)
                    .DisposeWith(disposables);
            });
        }

    }
}