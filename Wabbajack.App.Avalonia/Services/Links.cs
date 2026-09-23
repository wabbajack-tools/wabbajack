using System;
using System.Diagnostics;

namespace Wabbajack.App.Avalonia.Services;

public static class Links
{
    public static readonly Uri Wiki = new("https://wiki.wabbajack.org");
    public static readonly Uri ModlistWizard = new("https://wizard.wabbajack.org");
    public static readonly Uri GitHub = new("https://github.com/wabbajack-tools/wabbajack");
    public static readonly Uri Discord = new("https://discord.gg/wabbajack");
    public static readonly Uri Patreon = new("https://www.patreon.com/user?u=11907933");

    public static void Open(Uri uri)
        => Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
}
