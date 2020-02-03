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

            var manifestView = new ManifestView(Modlist);

            Grid.Children.Add(manifestView);

            Title = $"{Modlist.Name} by {Modlist.Author}";
        }
    }
}
