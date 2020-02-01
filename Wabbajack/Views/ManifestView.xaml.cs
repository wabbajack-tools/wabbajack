using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.View_Models;

namespace Wabbajack
{
    public partial class ManifestView
    {
        public ModList Modlist { get; set; }

        public ManifestView(ModList modlist)
        {
            Modlist = modlist;
            InitializeComponent();
            ViewModel = new ManifestVM(modlist);

            this.WhenActivated(disposable =>
            {
                this.Bind(ViewModel, x => x.Manifest.Name, x => x.Name.Text)
                    .DisposeWith(disposable);
            });
        }
    }
}
