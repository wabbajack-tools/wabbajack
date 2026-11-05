using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer;

public sealed class InstallFolderMarker
{
    public static readonly RelativePath FileName = (RelativePath) ".wabbajack-install.json";

    public string ModListName { get; set; } = "";
    public string? MachineUrl { get; set; }
    public string? Version { get; set; }
    public DateTime InstalledAt { get; set; }

    private static readonly JsonSerializerOptions Options = new() {WriteIndented = true};

    public static AbsolutePath PathIn(AbsolutePath install) => install.Combine(FileName);

    public static InstallFolderMarker? Read(AbsolutePath install)
    {
        var file = PathIn(install);
        if (!file.FileExists()) return null;

        try
        {
            return JsonSerializer.Deserialize<InstallFolderMarker>(file.ReadAllText(), Options);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task Write(AbsolutePath install, string modListName, string? machineUrl, string? version,
        CancellationToken token = default)
    {
        var marker = new InstallFolderMarker
        {
            ModListName = modListName,
            MachineUrl = string.IsNullOrWhiteSpace(machineUrl) ? null : machineUrl,
            Version = string.IsNullOrWhiteSpace(version) ? null : version,
            InstalledAt = DateTime.UtcNow
        };

        await PathIn(install).WriteAllTextAsync(JsonSerializer.Serialize(marker, Options), token);
    }
}
