using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens
{
    public partial class LauncherScreen : ScreenBase<LauncherViewModel>
    {
        public LauncherScreen()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.Image, view => view.SlideImage.Source)
                    .DisposeWith(disposables);
            });
        }

    }
}