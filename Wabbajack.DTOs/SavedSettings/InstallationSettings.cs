using System;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.SavedSettings;

public class InstallConfigurationState
{
    public AbsolutePath LastModlist { get; set; }

    public InstallationConfigurationSetting[] Settings { get; set; } =
        Array.Empty<InstallationConfigurationSetting>();
}

public class InstallationConfigurationSetting
{
    public AbsolutePath ModList { get; set; }
    public AbsolutePath Install { get; set; }
    public AbsolutePath Downloads { get; set; }

    public ModlistMetadata? Metadata { get; set; }

    public AbsolutePath Image { get; set; }
    public ModList? StrippedModListData { get; set; }
}