using System;

namespace Wabbajack.Networking.WabbajackClientApi;

public class Configuration
{
    public Uri ServerUri { get; set; } = new("https://build.wabbajack.org");
    public string MetricsKey { get; set; }
    public string MetricsKeyHeader { get; set; } = "x-metrics-key";

    public Uri ServerAllowList { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml");

    public Uri MirrorAllowList { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/allow-lists/main/allowed-mirrors.yaml");

    public Uri UpgradedArchives { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/upgraded.json");

    public Uri BuildServerUrl { get; set; } = new("https://build.wabbajack.org/");
    public string PatchBaseAddress { get; set; } = new("https://patches.wabbajack.org/");
}