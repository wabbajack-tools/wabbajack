using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Interventions;

public class ManualBlobDownload : AUserIntervention<Hash>
{
    public Archive Archive { get; }
    public AbsolutePath Destination { get; }

    public ManualBlobDownload(Archive archive, AbsolutePath destination)
    {
        Archive = archive;
        Destination = destination;
    }
}