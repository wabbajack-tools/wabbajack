using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.App.ViewModels;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens
{
    public partial class SettingsView : ScreenBase<SettingsViewModel>
    {
        public SettingsView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                this.BindCommand(ViewModel, vm => vm.NexusLogin, view => view.NexusLogIn)
                    .DisposeWith(disposables);
                this.BindCommand(ViewModel, vm => vm.NexusLogout, view => view.NexusLogOut)
                    .DisposeWith(disposables);

            });
        }

    }
}