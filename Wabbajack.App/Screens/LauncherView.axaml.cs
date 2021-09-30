using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens
{
    public partial class LauncherView : ScreenBase<LauncherViewModel>
    {
        public LauncherView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.Image, view => view.SlideImage.Source)
                    .DisposeWith(disposables);
                this.OneWayBind(ViewModel, vm => vm.Title, view => view.StatusText.Text)
                    .DisposeWith(disposables);
            });
        }

    }
}