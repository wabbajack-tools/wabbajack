using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.NexusApi
{
    public class RequestNexusAuthorization : AUserIntervention
    {
        public override string ShortDescription  => "Getting User's Nexus API Key";
        public override string ExtendedDescription { get; }

        private readonly TaskCompletionSource<string> _source = new TaskCompletionSource<string>();
        public Task<string> Task => _source.Task;

        public void Resume(string apikey)
        {
            Handled = true;
            _source.SetResult(apikey);
        }

        public override void Cancel()
        {
            Handled = true;
            _source.TrySetCanceled();
        }
    }
}
