using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Wabbajack.Services.OSIntegrated;

[SupportedOSPlatform("windows")]
public sealed class FileAssociationSelfHealService
{
    private readonly ILogger<FileAssociationSelfHealService> _logger;

    private const string ProgId = "Wabbajack.ModList";
    private const string Extension = ".wabbajack";
    private const string AppName = "Wabbajack";

    private const string Protocol = "wabbajack";

    public FileAssociationSelfHealService(ILogger<FileAssociationSelfHealService> logger)
    {
        _logger = logger;
    }

    public void RegisterOrUpdate(bool enableProtocol = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("File association self-heal skipped becaus not using Windows");
            return;
        }
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException("Process location is unavailable!");

        RegisterFileAssociation(exePath);

        if (enableProtocol)
            RegisterUrlProtocol(exePath);

        NotifyShellAssociationsChanged();

        _logger.LogInformation("File association complete. exe={ExePath}", exePath);
    }

    private void RegisterFileAssociation(string exePath)
    {
        var command = Quote(exePath) + " " + Quote("%1");

        using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}", true))
        {
            extKey!.SetValue("", ProgId);
        }

        using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}", true))
        {
            progKey!.SetValue("", $"{AppName} Modlist");

            using (var iconKey = progKey.CreateSubKey("DefaultIcon", true))
            {
                iconKey!.SetValue("", Quote(exePath) + ",0");
            }

            using (var cmdKey = progKey.CreateSubKey(@"shell\open\command", true))
            {
                cmdKey!.SetValue("", command);
            }
        }

        _logger.LogInformation("Registered per-user association: {Ext} : {ProgId} ({Command})", Extension, ProgId, command);
    }

    private void RegisterUrlProtocol(string exePath)
    {
        var command = Quote(exePath) + " " + Quote("%1");

        using var protoKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Protocol}", true);
        protoKey!.SetValue("", "URL:Wabbajack Protocol");
        protoKey.SetValue("URL Protocol", "");

        using (var iconKey = protoKey.CreateSubKey("DefaultIcon", true))
        {
            iconKey!.SetValue("", Quote(exePath) + ",0");
        }

        using (var cmdKey = protoKey.CreateSubKey(@"shell\open\command", true))
        {
            cmdKey!.SetValue("", command);
        }

        _logger.LogInformation("Registered per-user URL protocol: {Protocol}:// ({Command})", Protocol, command);
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    private static void NotifyShellAssociationsChanged()
    {
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
