using System.Collections.Generic;
using System.CommandLine;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CefNet.Avalonia;
using ReactiveUI;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;
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