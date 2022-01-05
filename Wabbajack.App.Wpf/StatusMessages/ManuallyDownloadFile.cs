using System;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack
{
    public class ManuallyDownloadFile : AUserIntervention
    {
        public Manual State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private readonly TaskCompletionSource<(Uri, HttpResponseMessage)> _tcs = new ();
        public Task<(Uri, HttpResponseMessage)> Task => _tcs.Task;

        private ManuallyDownloadFile(Manual state)
        {
            State = state;
        }

        public static async Task<ManuallyDownloadFile> Create(Manual state)
        {
            var result = new ManuallyDownloadFile(state);
            return result;
        }
        public override void Cancel()
        {
            _tcs.SetCanceled();
        }

        public void Resume(Uri s, HttpResponseMessage client)
        {
            _tcs.SetResult((s, client));
        }
    }
}
