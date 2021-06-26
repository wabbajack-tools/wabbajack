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
        private const string ModlistStatusListUrl = "https://build.wabbajack.org/lists/status.json";
        
        private readonly HttpClient _client;
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            IgnoreNullValues = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public StateContainer(HttpClient client)
        {
            _client = client;
        }

        #region ModlistMetadata
        
        private List<ModlistMetadata> _modlists = new();
        public IReadOnlyList<ModlistMetadata> Modlists => _modlists;
        public bool HasModlistMetadata => _modlists.Any();
        
        public ModlistMetadata? GetModlistByMachineUrl(string machineUrl)
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
            var res = await _client.GetFromJsonAsync<List<ModlistMetadata>>(
                ModlistsJsonUrl,
                JsonSerializerOptions,
                CancellationToken.None);

            return res;
        }

        #endregion

        #region ModlistStatus

        private List<ModlistStatus> _modlistStatusList = new();
        public IReadOnlyList<ModlistStatus> ModlistStatusList => _modlistStatusList;
        public bool HasModlistStatusList => _modlistStatusList.Any();

        public ModlistStatus? GetStatusByMachineUrl(string machineUrl)
        {
            return _modlistStatusList.FirstOrDefault(x =>
                x.MachineUrl != null &&
                x.MachineUrl.Equals(machineUrl, StringComparison.OrdinalIgnoreCase));
        }
        
        public async Task<bool> RefreshModlistStatus()
        {
            var res = await GetModlistStatusList();
            if (res == null) return false;
            
            _modlistStatusList = res;
            return true;
        }
        
        private async Task<List<ModlistStatus>?> GetModlistStatusList()
        {
            var res = await _client.GetFromJsonAsync<List<ModlistStatus>>(
                ModlistStatusListUrl,
                JsonSerializerOptions,
                CancellationToken.None);

            return res;
        }
        
        #endregion
        
    }
}
