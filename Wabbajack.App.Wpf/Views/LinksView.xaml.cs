using System.Windows;
using System.Windows.Controls;

namespace Wabbajack;

/// <summary>
/// Interaction logic for LinksView.xaml
/// </summary>
public partial class LinksView : UserControl
{
    public LinksView()
    {
        InitializeComponent();
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
        => UIUtils.OpenWebsite(Consts.WabbajackGithubUri);

    private void Discord_Click(object sender, RoutedEventArgs e)
        => UIUtils.OpenWebsite(Consts.WabbajackDiscordUri);

    private void Patreon_Click(object sender, RoutedEventArgs e)
        => UIUtils.OpenWebsite(Consts.WabbajackPatreonUri);

    private void Wiki_Click(object sender, RoutedEventArgs e)
        => UIUtils.OpenWebsite(Consts.WabbajackWikiUri);
}
