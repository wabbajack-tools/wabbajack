using System;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
{
    public class ManuallyDownloadMegaFile : AUserIntervention
    {
        public ManualDownloader.State State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private TaskCompletionSource _tcs = new();
        public Task Task => _tcs.Task;
        
        
        public AbsolutePath Destination { get; }

        private ManuallyDownloadMegaFile(ManualDownloader.State state, AbsolutePath destination)
        {
            State = state;
            Destination = destination;
        }


        public static async Task<ManuallyDownloadMegaFile> Create(ManualDownloader.State state, AbsolutePath destination)
        {
            var result = new ManuallyDownloadMegaFile(state, destination);
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
