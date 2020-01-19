using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using ReactiveUI;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
    {
        public ModListGalleryView()
        {
            InitializeComponent();

            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.BackCommand)
                    .BindToStrict(this, x => x.BackButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ModLists)
                    .BindToStrict(this, x => x.ModListGalleryControl.ItemsSource)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ModLists.Count)
                    .Select(x => x > 0 ? Visibility.Collapsed : Visibility.Visible)
                    .BindToStrict(this, x => x.LoadingRing.Visibility)
                    .DisposeWith(dispose);
            });
        }
    }
}
