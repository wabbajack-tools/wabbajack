using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public class SubStage : ASubStage
    {
        public SubStage(RelativePath src, AbsolutePath destination, IEnumerable<IStagingPlan> plans) : base(plans)
        {
            Source = src;
            Destination = destination;
        }
        
        public override async ValueTask DisposeAsync()
        {
        }
        public override AbsolutePath Destination { get; }
        public override IPath Source { get; }
    }
}
