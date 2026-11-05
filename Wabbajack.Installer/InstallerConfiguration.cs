using System;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;

namespace Wabbajack.Installer;

public class InstallerConfiguration
{
    public AbsolutePath ModlistArchive { get; set; }
    public ModList ModList { get; set; }
    public AbsolutePath Install { get; set; }
    public AbsolutePath Downloads { get; set; }
    public SystemParameters? SystemParameters { get; set; }
    public Game Game { get; set; }
    public Game[]? OtherGames { get; set; } = Array.Empty<Game>();
    public AbsolutePath GameFolder { get; set; }

    public ModlistMetadata? Metadata { get; set; }

    public AbsolutePath PreviousInstallLocation { get; set; }

    public InstallFolderVerdict? PreflightInstallFolder { get; set; }
}