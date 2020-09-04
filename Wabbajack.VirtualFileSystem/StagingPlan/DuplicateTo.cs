using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public class DuplicateTo : IStagingPlan
    {
        private readonly IStagingPlan _src;

        public DuplicateTo(IStagingPlan src, AbsolutePath destination)
        {
            _src = src;
            Destination = destination;
        }
        public async ValueTask DisposeAsync()
        {
        }

        public async Task Execute()
        {
            await _src.Destination.CopyToAsync(Destination);
        }

        public AbsolutePath Destination { get; }
    }
}
