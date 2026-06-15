using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

public partial class LinksView : UserControl
{
    public LinksView() => AvaloniaXamlLoader.Load(this);

    private static void Open(Uri url) =>
        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

    private void Patreon_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackPatreonUri);
    private void GitHub_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackGithubUri);
    private void Discord_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackDiscordUri);
    private void Wiki_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackWikiUri);
}
