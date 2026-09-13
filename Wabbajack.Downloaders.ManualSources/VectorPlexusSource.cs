using System;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.ManualSources;

public class VectorPlexusSource : AIPS4Source<VectorPlexus>
{
    public VectorPlexusSource() : base(new Uri("https://vectorplexis.com"), "Vector Plexus")
    {
    }
}
