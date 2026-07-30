using Avalonia.Controls;

namespace Wabbajack;

public partial class LoginWindowView : Window
{
    public LoginWindowView()
    {
        InitializeComponent();
    }

    /*
    public INeedsLoginCredentials Downloader { get; set; }

    public LoginWindowView(INeedsLoginCredentials downloader)
    {
        Downloader = downloader;

        InitializeComponent();

        var loginView = new CredentialsLoginView(downloader);

        Grid.Children.Add(loginView);
    }
    */
}
