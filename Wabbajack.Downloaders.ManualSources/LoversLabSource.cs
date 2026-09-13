using System;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.ManualSources;

public class LoversLabSource : AIPS4Source<LoversLab>
{
    public LoversLabSource() : base(new Uri("https://api.loverslab.com"), "Lovers Lab")
    {
    }
}
