using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Browser;

public class Configuration
{
    public AbsolutePath HostExecutable { get; set; } =
        KnownFolders.EntryPoint.Combine("Wabbajack.Networking.Browser.Host.exe");
}