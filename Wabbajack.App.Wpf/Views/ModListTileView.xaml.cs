using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using MahApps.Metro.IconPacks;
using ReactiveUI;
using Wabbajack.DTOs;

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
            this.WhenActivated(disposables =>
            {
                ViewModel.WhenAnyValue(vm => vm.Image)
                         .BindToStrict(this, v => v.ModlistImage.ImageSource)
                         .DisposeWith(disposables);
                        
                /*
                this.WhenAny(x => x.ViewModel.Metadata.Links.ImageUri)
                    .Select(x => new BitmapImage() { UriSource = new Uri(x) })
                    .BindToStrict(this, v => v.ModlistImage.ImageSource)
                    .DisposeWith(disposables);
                */
                /*
                ViewModel.WhenAnyValue(x => x.Metadata.Links.ImageUri)
                         .Select(x => {
                             var img = new BitmapImage();
                             img.BeginInit();
                             img.CacheOption = BitmapCacheOption.OnDemand;
                             img.DecodePixelWidth = 327;
                             var uri = new Uri(x, UriKind.Absolute);
                             img.UriSource = uri;
                             img.EndInit();

                             return img;
                         })
                         .BindToStrict(this, v => v.ModlistImage.ImageSource)
                         .DisposeWith(disposables);
                */


                var textXformed = ViewModel.WhenAnyValue(vm => vm.Metadata.Title)
                    .CombineLatest(ViewModel.WhenAnyValue(vm => vm.Metadata.ImageContainsTitle),
                                ViewModel.WhenAnyValue(vm => vm.IsBroken))
                    .Select(x => x.Second && !x.Third ? "" : x.First);


                /*
                textXformed
                    .BindToStrict(this, view => view.ModListTitle.Text)
                    .DisposeWith(disposables);

                textXformed
                    .BindToStrict(this, view => view.ModListTitleShadow.Text)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(x => x.Metadata.Description)
                    .BindToStrict(this, x => x.MetadataDescription.Text)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(x => x.ModListTagList)
                    .BindToStrict(this, x => x.TagsList.ItemsSource)
                    .DisposeWith(disposables);
                */
                
                ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.LoadingProgress.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(x => x.IsBroken)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.Overlay.Visibility)
                    .DisposeWith(disposables);
                
                /*
                ViewModel.WhenAnyValue(x => x.OpenWebsiteCommand)
                    .BindToStrict(this, x => x.OpenWebsiteButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(x => x.ModListContentsCommend)
                    .BindToStrict(this, x => x.ModListContentsButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(x => x.ExecuteCommand)
                    .BindToStrict(this, x => x.ExecuteButton.Command)
                    .DisposeWith(disposables);
                */

                
                /*
                ViewModel.WhenAnyValue(x => x.ProgressPercent)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Select(p => p.Value)
                    .BindTo(this, x => x.DownloadProgressBar.Value)
                    .DisposeWith(disposables);
                */

                /*
                ViewModel.WhenAnyValue(x => x.Status)
                    .ObserveOnGuiThread()
                    .Subscribe(x =>
                    {
                        IconContainer.Children.Clear();
                        IconContainer.Children.Add(new PackIconMaterial
                        {
                            Width = 20,
                            Height = 20,
                            Kind = x switch
                            {
                                ModListMetadataVM.ModListStatus.Downloaded => PackIconMaterialKind.Play,
                                ModListMetadataVM.ModListStatus.Downloading => PackIconMaterialKind.Network,
                                ModListMetadataVM.ModListStatus.NotDownloaded => PackIconMaterialKind.Download,
                                _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
                            }
                        });
                    })
                    .DisposeWith(disposables);
                */

                /*
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, bool>(this.ViewModel, x => x.IsBroken);
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, bool>(this.ViewModel, x => x.Exists);
                this.MarkAsNeeded<ModListTileView, ModListMetadataVM, string>(this.ViewModel, x => x.Metadata.Links.ImageUri);
                this.WhenAny(x => x.ViewModel.ProgressPercent)
                    .Select(p => p.Value)
                    .BindToStrict(this, x => x.DownloadProgressBar.Value)
                    .DisposeWith(dispose);


                this.WhenAny(x => x.ViewModel.ModListContentsCommend)
                    .BindToStrict(this, x => x.ModListContentsButton.Command)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.ViewModel.Image)
                    .BindToStrict(this, x => x.ModListImage.Source)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.LoadingImage)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.LoadingProgress.Visibility)
                    .DisposeWith(dispose);
                    */
            });
        }
    }
}
