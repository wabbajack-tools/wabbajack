using System;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack
{
    public class ManuallyDownloadNexusFile : AUserIntervention
    {
        public Nexus State { get; }
        public override string ShortDescription { get; } = string.Empty;
        public override string ExtendedDescription { get; } = string.Empty;

        private TaskCompletionSource<Uri> _tcs = new TaskCompletionSource<Uri>();
        public Task<Uri> Task => _tcs.Task;

        private ManuallyDownloadNexusFile(Nexus state)
        {
            State = state;
        }

        public static async Task<ManuallyDownloadNexusFile> Create(Nexus state)
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
