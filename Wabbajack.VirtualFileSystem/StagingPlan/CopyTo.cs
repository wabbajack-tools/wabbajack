using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public class CopyTo : IStagingSrc
    {
        public CopyTo(IPath src, AbsolutePath destination)
        {
            Destination = destination;
            Source = src;
        }

        public AbsolutePath Destination { get; }

        public async ValueTask DisposeAsync()
        {
            
        }

        public IPath Source { get; }
    }
}
