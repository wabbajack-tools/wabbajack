using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModlistPropertiesWindow.xaml
    /// </summary>
    public partial class ModlistPropertiesWindow : Window
    {
        internal string newBannerFile;
        internal readonly AppState state;
        internal ModlistPropertiesWindow(AppState _state)
        {
            InitializeComponent();
            var bannerImage = UIUtils.BitmapImageFromResource("Wabbajack.UI.banner.png");
            SplashScreenProperty.Source = bannerImage;

            newBannerFile = null;
            state = _state;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //Hide();
        }

        private void SetSplashScreen_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog("Banner image|*.png");
            if (file != null)
            {
                newBannerFile = file;
                SplashScreenProperty.Source = new BitmapImage(new Uri(file));
            }
        }

        private void SaveProperties_Click(object sender, RoutedEventArgs e)
        {
            if (state.UIReady)
            {
                if (newBannerFile != null)
                {
                    BitmapImage splashScreen = new BitmapImage(new Uri(newBannerFile));
                    state.newImagePath = newBannerFile;
                    state.SplashScreenImage = splashScreen;
                }
                string modListName = ModlistNameProperty.Text;
                string modListAuthor = ModlistAuthorProperty.Text;
                string modListDescription = ModlistDescriptionProperty.Text;
                string modListWebsite = ModlistWebsiteProperty.Text;

                state.SplashScreenModName = modListName;
                state.SplashScreenSummary = modListDescription;
                state.SplashScreenAuthorName = modListAuthor;
                state._nexusSiteURL = modListWebsite;

                state.ChangedProperties = true;

                Hide();
            }
        }
    }
}
