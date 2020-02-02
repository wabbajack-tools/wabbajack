using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Windows.Documents;
using System.Windows.Navigation;
using Microsoft.VisualBasic.CompilerServices;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Utils = Wabbajack.Common.Utils;

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
                this.OneWayBind(ViewModel, x => x.Archives, x => x.ModsList.ItemsSource)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.InstallSize, x => x.InstallSize.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.DownloadSize, x => x.DownloadSize.Text)
                    .DisposeWith(disposable);
            });
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (!(sender is Hyperlink hyperlink)) return;
            if (!(hyperlink.DataContext is Archive archive)) return;

            var url = archive.State.GetManifestURL(archive);
            if (string.IsNullOrWhiteSpace(url)) return;

            //url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});

            e.Handled = true;
        }
    }
}
