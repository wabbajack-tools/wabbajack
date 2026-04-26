using Wabbajack.Paths;

namespace Wabbajack.DTOs.Interventions;

/// <summary>
/// Download file to specified destination; verifying to be done by caller, so returns if download succeeded at all
/// </summary>
public class ManualBrowserDownload : AUserIntervention<bool>
{
    public Archive Archive { get; }
    public AbsolutePath Destination { get; }

    public ManualBrowserDownload(Archive archive, AbsolutePath destination)
    {
        Archive = archive;
        Destination = destination;
    }
}