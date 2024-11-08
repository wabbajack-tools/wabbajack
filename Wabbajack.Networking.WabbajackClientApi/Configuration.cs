using System;

namespace Wabbajack.Networking.WabbajackClientApi;

public class Configuration
{
    public Uri MirrorServerUrl { get; set; } = new ("https://mirror.wabbajack.org");
    public Uri ServerUri { get; set; } = new("https://build.wabbajack.org");
    public string MetricsKey { get; set; }
    public string MetricsKeyHeader { get; set; } = "x-metrics-key";
    public string AuthorKeyHeader { get; set; } = "x-api-key";

    public string ResponseShaHeader { get; set; } = "x-content-sha";


    public Uri ServerAllowList { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml");
    
    public Uri MirrorList { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/mirrors.json");

    public Uri MirrorAllowList { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/allow-lists/main/allowed-mirrors.yaml");

    public Uri UpgradedArchives { get; set; } =
        new("https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/upgraded.json");

    public Uri BuildServerUrl { get; set; } = new("https://build.wabbajack.org/");
    //public Uri BuildServerUrl { get; set; } = new("http://localhost:5000/");
    public string PatchBaseAddress { get; set; } = new("https://patches.wabbajack.org/");
}