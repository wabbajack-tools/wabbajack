using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public class NativeArchive : ASubStage
    {
        public NativeArchive(AbsolutePath src, IEnumerable<IStagingPlan> plans) : base(plans)
        {
            Source = src;
            Destination = src;
        }

        public override async ValueTask DisposeAsync()
        {
        }

        public override AbsolutePath Destination { get; }
        public override IPath Source { get; }
    }
}
