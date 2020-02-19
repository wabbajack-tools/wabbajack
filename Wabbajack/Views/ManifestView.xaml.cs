using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public partial class ManifestView
    {
        public ModList Modlist { get; set; }

        public ManifestView(ModList modlist)
        {
            Modlist = modlist;

            var manifest = new Manifest(modlist);
            if(ViewModel == null)
                ViewModel = new ManifestVM(manifest);

            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                this.OneWayBind(ViewModel, x => x.Name, x => x.Name.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.Author, x => x.Author.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.Description, x => x.Description.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.SearchResults, x => x.ModsList.ItemsSource)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.InstallSize, x => x.InstallSize.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.DownloadSize, x => x.DownloadSize.Text)
                    .DisposeWith(disposable);
                this.Bind(ViewModel, x => x.SearchTerm, x => x.SearchBar.Text)
                    .DisposeWith(disposable);
                this.BindCommand(ViewModel, x => x.SortByNameCommand, x => x.OrderByNameButton)
                    .DisposeWith(disposable);
                this.BindCommand(ViewModel, x => x.SortBySizeCommand, x => x.OrderBySizeButton)
                    .DisposeWith(disposable);
            });
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (!(sender is Hyperlink hyperlink)) return;
            if (!(hyperlink.DataContext is Archive archive)) return;

            var url = archive.State.GetManifestURL(archive);
            if (string.IsNullOrWhiteSpace(url)) return;

            if (url.StartsWith("https://github.com/"))
                url = url.Substring(0, url.IndexOf("release", StringComparison.Ordinal));

            //url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});

            e.Handled = true;
        }

        //solution from https://stackoverflow.com/questions/5426232/how-can-i-make-wpf-scrollviewer-middle-click-scroll/5446307#5446307

        private bool _isMoving;                  //False - ignore mouse movements and don't scroll
        private bool _isDeferredMovingStarted;   //True - Mouse down -> Mouse up without moving -> Move; False - Mouse down -> Move
        private Point? _startPosition;
        private const double Slowdown = 10;      //smaller = faster

        private void ScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isMoving)
                CancelScrolling();
            else if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                if (_isMoving) return;

                _isMoving = true;
                _startPosition = e.GetPosition(sender as IInputElement);
                _isDeferredMovingStarted = true;

                AddScrollSign(e.GetPosition(TopLayer).X, e.GetPosition(TopLayer).Y);
            }
        }

        private void ScrollViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released && _isDeferredMovingStarted != true)
                CancelScrolling();
        }
        
        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMoving || !(sender is ScrollViewer sv))
                return;

            _isDeferredMovingStarted = false;

            var currentPosition = e.GetPosition(sv);
            if (_startPosition == null)
                return;

            var offset = currentPosition - _startPosition.Value;
            offset.Y /= Slowdown;
            offset.X /= Slowdown;

            sv.ScrollToVerticalOffset(sv.VerticalOffset + offset.Y);
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset + offset.X);
        }

        private void CancelScrolling()
        {
            _isMoving = false;
            _startPosition = null;
            _isDeferredMovingStarted = false;
            RemoveScrollSign();
        }

        private void AddScrollSign(double x, double y)
        {
            const double size = 50.0;
            var img = ResourceLinks.MiddleMouseButton.Value;
            var icon = new Image {Source = img, Width = size, Height = size};
            //var icon = new Ellipse { Stroke = Brushes.Red, StrokeThickness = 2.0, Width = 20, Height = 20 };

            TopLayer.Children.Add(icon);
            Canvas.SetLeft(icon, x - size / 2);
            Canvas.SetTop(icon, y - size / 2);
        }

        private void RemoveScrollSign()
        {
            TopLayer.Children.Clear();
        }
    }
}
