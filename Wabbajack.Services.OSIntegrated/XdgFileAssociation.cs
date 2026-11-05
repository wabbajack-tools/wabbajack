using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Services.OSIntegrated;

/// <summary>
///     The Linux side of <see cref="FileAssociationSelfHealService" />: a desktop entry that opens .wabbajack files
///     and wabbajack:// links with this executable, and the MIME type that makes a .wabbajack file one. Everything
///     goes under the user's own data folder (<c>$XDG_DATA_HOME</c>, else <c>~/.local/share</c>), so nothing needs
///     root, and it is rewritten on every start so the entry follows the executable wherever it moves.
/// </summary>
public sealed class XdgFileAssociation
{
    public const string DesktopFileName = "wabbajack.desktop";
    public const string MimeType = "application/x-wabbajack";
    public const string SchemeMimeType = "x-scheme-handler/wabbajack";

    private readonly ILogger<XdgFileAssociation> _logger;

    public XdgFileAssociation(ILogger<XdgFileAssociation> logger)
    {
        _logger = logger;
    }

    public void RegisterOrUpdate()
    {
        if (!OperatingSystem.IsLinux()) return;

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrWhiteSpace(dataHome))
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local",
                    "share");

            var applications = Path.Combine(dataHome, "applications");
            var mimePackages = Path.Combine(dataHome, "mime", "packages");
            Directory.CreateDirectory(applications);
            Directory.CreateDirectory(mimePackages);

            File.WriteAllText(Path.Combine(applications, DesktopFileName), DesktopEntry(exePath));
            File.WriteAllText(Path.Combine(mimePackages, "wabbajack.xml"), MimePackage());

            // Each of these only refreshes a cache or a default; a desktop without them still has the files above.
            Run("update-mime-database", Path.Combine(dataHome, "mime"));
            Run("update-desktop-database", applications);
            Run("xdg-mime", "default", DesktopFileName, MimeType, SchemeMimeType);

            _logger.LogInformation("Registered {DesktopFile} for {MimeType} and {Scheme}", DesktopFileName, MimeType,
                SchemeMimeType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register .wabbajack files and wabbajack:// links with the desktop");
        }
    }

    /// <summary>The desktop entry, with the executable quoted as the spec asks for paths with spaces.</summary>
    public static string DesktopEntry(string exePath)
    {
        var exec = "\"" + exePath.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`")
            .Replace("$", "\\$") + "\"";
        return "[Desktop Entry]\n" +
               "Type=Application\n" +
               "Name=Wabbajack\n" +
               "Comment=Automated modlist installer\n" +
               $"Exec={exec} %u\n" +
               $"MimeType={MimeType};{SchemeMimeType};\n" +
               "Categories=Game;Utility;\n" +
               "Terminal=false\n";
    }

    public static string MimePackage()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               "<mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\">\n" +
               $"  <mime-type type=\"{MimeType}\">\n" +
               "    <comment>Wabbajack modlist</comment>\n" +
               "    <glob pattern=\"*.wabbajack\"/>\n" +
               "  </mime-type>\n" +
               "</mime-info>\n";
    }

    private void Run(string program, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(program) {UseShellExecute = false, CreateNoWindow = true};
            foreach (var arg in args) info.ArgumentList.Add(arg);
            using var process = Process.Start(info);
            process?.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Program} is not available", program);
        }
    }
}
