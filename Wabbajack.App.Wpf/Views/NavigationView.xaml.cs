using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for NavigationView.xaml
    /// </summary>
    public partial class NavigationView : ReactiveUserControl<NavigationVM>
    {
        public NavigationView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.BindCommand(ViewModel, vm => vm.BrowseCommand, v => v.BrowseButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.HomeCommand, v => v.HomeButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.CompileCommand, v => v.CompileButton)
                    .DisposeWith(dispose);
                this.BindCommand(ViewModel, vm => vm.SettingsCommand, v => v.SettingsButton)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.ViewModel.Version)
                    .Select(version => $"v{version}")
                    .BindToStrict(this, v => v.VersionTextBlock.Text)
                    .DisposeWith(dispose);
                /*
                this.WhenAny(x => x.ViewModel.InstallCommand)
                    .BindToStrict(this, x => x.InstallButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.CompileCommand)
                    .BindToStrict(this, x => x.CompileButton.Command)
                    .DisposeWith(dispose);
                */
            });
        }
    }
}
