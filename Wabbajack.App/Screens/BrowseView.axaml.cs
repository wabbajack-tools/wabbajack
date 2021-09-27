using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using Wabbajack.App.ViewModels;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens
{
    public partial class BrowseView : ScreenBase<BrowseViewModel>
    {
        public BrowseView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm.ModLists, view => view.GalleryList.Items)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SearchText, view => view.SearchBox.Text)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel, vm => vm.GamesList, view => view.GamesList.Items)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.SelectedGame, view => view.GamesList.SelectedItem)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel, vm => vm.ResetFiltersCommand, view => view.ClearFiltersButton)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.OnlyInstalledGames, view => view.OnlyInstalledCheckbox.IsChecked)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.OnlyUtilityLists, view => view.ShowUtilityLists.IsChecked)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.ShowNSFW, view => view.ShowNSFW.IsChecked)
                    .DisposeWith(disposables);
            });
        }
    }
}