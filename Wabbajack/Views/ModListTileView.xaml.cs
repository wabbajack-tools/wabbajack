using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModListTileView.xaml
    /// </summary>
    public partial class ModListTileView : ReactiveUserControl<ModListMetadataVM>
    {
        public ModListTileView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, double>(this.ViewModel, x => x.ProgressPercent);
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, bool>(this.ViewModel, x => x.IsBroken);
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, bool>(this.ViewModel, x => x.Exists);
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, string>(this.ViewModel, x => x.Metadata.Links.ImageUri);
                this.WhenAny(x => x.ViewModel.Metadata.Title)
                    .BindToStrict(this, x => x.DescriptionTextShadow.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Metadata.Title)
                    .BindToStrict(this, x => x.ModListTitleShadow.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.IsBroken)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.Overlay.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Metadata.Description)
                    .BindToStrict(this, x => x.MetadataDescription.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.OpenWebsiteCommand)
                    .BindToStrict(this, x => x.OpenWebsiteButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ExecuteCommand)
                    .BindToStrict(this, x => x.ExecuteButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Image)
                    .BindToStrict(this, x => x.ModListImage.Source)
                    .DisposeWith(dispose);
            });
        }
    }
}
