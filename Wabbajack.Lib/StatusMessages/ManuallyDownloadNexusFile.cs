using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
{
    public class ManuallyDownloadNexusFile : AUserIntervention
    {
        public  NexusDownloader.State State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private TaskCompletionSource<Uri> _tcs = new TaskCompletionSource<Uri>();
        public Task<Uri> Task => _tcs.Task;

        private ManuallyDownloadNexusFile(NexusDownloader.State state)
        {
            State = state;
        }

        public static async Task<ManuallyDownloadNexusFile> Create(NexusDownloader.State state)
        {
            var result = new ManuallyDownloadNexusFile(state);
            return result;
        }
        public override void Cancel()
        {
            _tcs.SetCanceled();
        }

        public void Resume(Uri s)
        {
            _tcs.SetResult(s);
        }
    }
}
