using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public interface IStagingSrc : IStagingPlan
    {
        public IPath Source { get; }
    }
}
