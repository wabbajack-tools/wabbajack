using System;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
{
    public class ManuallyDownloadFile : AUserIntervention
    {
        public ManualDownloader.State State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private TaskCompletionSource<(Uri, Common.Http.Client)> _tcs = new TaskCompletionSource<(Uri, Common.Http.Client)>();
        public Task<(Uri, Common.Http.Client)> Task => _tcs.Task;

        private ManuallyDownloadFile(ManualDownloader.State state)
        {
            State = state;
        }

        public static async Task<ManuallyDownloadFile> Create(ManualDownloader.State state)
        {
            var result = new ManuallyDownloadFile(state);
            return result;
        }
        public override void Cancel()
        {
            _tcs.SetCanceled();
        }

        public void Resume(Uri s, Common.Http.Client client)
        {
            _tcs.SetResult((s, client));
        }
    }
}
