using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Web.DTO;

#nullable enable

namespace Wabbajack.Web.Services
{
    public class StateContainer
    {
        private const string ModlistsJsonUrl = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/modlists.json";
        private readonly HttpClient _client;

        public StateContainer(HttpClient client)
        {
            _client = client;
        }
        
        private List<ModlistMetadata> _modlists = new();
        public IReadOnlyList<ModlistMetadata> Modlists => _modlists;

        public bool HasModlistMetadata => _modlists.Any();

        public ModlistMetadata? GetByMachineUrl(string machineUrl)
        {
            return _modlists.FirstOrDefault(x =>
                x.Links?.MachineUrl != null &&
                x.Links.MachineUrl.Equals(machineUrl, StringComparison.OrdinalIgnoreCase));
        }
        
        public async Task<bool> RefreshMetadata()
        {
            var res = await GetModlistMetadata();
            if (res == null) return false;
            
            _modlists = res;
            return true;
        }
        
        private async Task<List<ModlistMetadata>?> GetModlistMetadata()
        {
            var res = await _client.GetFromJsonAsync<List<ModlistMetadata>>(ModlistsJsonUrl, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                IgnoreNullValues = true,
            }, CancellationToken.None);

            return res;
        }
    }
}
