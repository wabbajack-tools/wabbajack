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
                Observable.CombineLatest(
                        this.WhenAny(x => x.ViewModel.ModLists.Count)
                            .Select(x => x > 0),
                        this.WhenAny(x => x.ViewModel.Error)
                            .Select(e => e?.Succeeded ?? true),
                        resultSelector: (hasContent, succeeded) =>
                        {
                            return !hasContent && succeeded;
                        })
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .StartWith(Visibility.Collapsed)
                    .BindToStrict(this, x => x.LoadingRing.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Error)
                    .Select(e => (e?.Succeeded ?? true) ? Visibility.Collapsed : Visibility.Visible)
                    .StartWith(Visibility.Collapsed)
                    .BindToStrict(this, x => x.ErrorIcon.Visibility)
                    .DisposeWith(dispose);
            });
        }
    }
}
