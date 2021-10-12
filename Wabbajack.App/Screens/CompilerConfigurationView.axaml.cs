

using System.Reactive.Disposables;
using Avalonia;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens
{
    public partial class CompilerConfigurationView : ScreenBase<CompilerConfigurationViewModel>
    {
        public CompilerConfigurationView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                                
                this.Bind(ViewModel, vm => vm.BasePath, view => view.BaseFolder.SelectedPath)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.SettingsFile, view => view.SettingsFile.SelectedPath)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.Downloads, view => view.DownloadsFolder.SelectedPath)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.AllGames, view => view.BaseGame.Items)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.BaseGame, view => view.BaseGame.SelectedItem)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.StartCompilation, view => view.StartCompilation)
                    .DisposeWith(disposables);


            });
        }
    }
}