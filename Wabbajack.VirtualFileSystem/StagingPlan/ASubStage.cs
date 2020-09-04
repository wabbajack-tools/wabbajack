using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public abstract class ASubStage : ISubStage
    {
        private IEnumerable<IStagingPlan> _plans;

        public ASubStage(IEnumerable<IStagingPlan> plans)
        {
            _plans = plans;
        }
        
        public abstract ValueTask DisposeAsync();
        public abstract AbsolutePath Destination { get; }
        public abstract IPath Source { get; }
        
        public async Task Execute(WorkQueue queue)
        {
            await StagingPlan.ExecutePlan(queue, async () => await Destination.OpenWrite(), _plans);
        }
    }
}
