using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public class TempSubStage : ASubStage
    {
        private readonly TempFile _temp;

        public TempSubStage(RelativePath src, IEnumerable<IStagingPlan> plans) : base(plans)
        {
            Source = src;
            _temp = new TempFile();
        }
        
        public override async ValueTask DisposeAsync()
        {
           await _temp.DisposeAsync();
        }

        public override AbsolutePath Destination => _temp.Path;
        public override IPath Source { get; }
    }
}
