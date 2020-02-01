using Wabbajack.Lib;

namespace Wabbajack
{
    public partial class ManifestWindow
    {
        public ModList Modlist { get; set; }

        public ManifestWindow(ModList modlist)
        {
            Modlist = modlist;
            InitializeComponent();
            Grid.Children.Add(new ManifestView(Modlist));
        }
    }
}
