using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.Networking.Browser.ViewModels;

namespace Wabbajack.Networking.Browser.Views
{
    public partial class MainWindow
        : ReactiveWindow<MainWindowViewModel>
    {

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.WhenActivated(disposables =>
            {
                ViewModel.WhenAnyValue(vm => vm.Instructions)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Select(v => v)
                    .BindTo(this, view => view.Instructions.Text)
                    .DisposeWith(disposables);

            });
        }

    }
}