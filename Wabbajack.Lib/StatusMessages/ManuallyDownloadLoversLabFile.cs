using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
{
    public class ManuallyDownloadLoversLabFile : AUserIntervention
    {
        public ManualDownloader.State State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private TaskCompletionSource _tcs = new();
        public Task Task => _tcs.Task;
        
        
        public AbsolutePath Destination { get; }
        public Archive Archive { get; }

        private ManuallyDownloadLoversLabFile(ManualDownloader.State state, AbsolutePath destination, Archive archive)
        {
            State = state;
            Destination = destination;
            Archive = archive;
        }
        
        public static async Task<ManuallyDownloadLoversLabFile> Create(ManualDownloader.State state, AbsolutePath destination, Archive archive)
        {
            var result = new ManuallyDownloadLoversLabFile(state, destination, archive);
            return result;
        }
        public override void Cancel()
        {
            _tcs.SetCanceled();
        }

        public void Resume()
        {
            _tcs.SetResult();
        }
    }
}
