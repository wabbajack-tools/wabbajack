using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.StagingPlan
{
    public interface ISubStage : IStagingSrc
    {
        Task Execute(WorkQueue queue);
    }
}
