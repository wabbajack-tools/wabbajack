using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Models
{
    public class InstallationStateManager
    {
        private static AbsolutePath Path => KnownFolders.WabbajackAppLocal.Combine("install-configuration-state.json");
        private readonly DTOSerializer _dtos;
        private readonly ILogger<InstallationStateManager> _logger;

        public InstallationStateManager(ILogger<InstallationStateManager> logger, DTOSerializer dtos)
        {
            _dtos = dtos;
            _logger = logger;
        }

        public async Task<InstallationConfigurationSetting> GetLastState()
        {
            var state = await GetAll();
            var result = state.Settings.FirstOrDefault(s => s.ModList == state.LastModlist) ?? 
                         new InstallationConfigurationSetting();

            if (!result.ModList.FileExists())
                return new InstallationConfigurationSetting();
            return result;
        }

        public async Task SetLastState(InstallationConfigurationSetting setting)
        {
            if (!setting.ModList.FileExists())
            {
                _logger.LogCritical("ModList path doesn't exist, not saving settings");
                return;
            }
            
            var state = await GetAll();
            state.LastModlist = setting.ModList;
            state.Settings = state.Settings
                .Where(s => s.ModList != setting.ModList)
                .Append(setting)
                .ToArray();
            
            await using var fs = Path.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await _dtos.Serialize(state, fs, true);
        }

        public async Task<InstallConfigurationState> GetAll()
        {
            if (!Path.FileExists()) return new InstallConfigurationState();

            try
            {
                await using var fs = Path.Open(FileMode.Open);
                return (await _dtos.DeserializeAsync<InstallConfigurationState>(fs))!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While loading json");
                return new InstallConfigurationState();
            }

        }

        public async Task<InstallationConfigurationSetting?> Get(AbsolutePath modListPath)
        {
            return (await GetAll()).Settings.FirstOrDefault(f => f.ModList == modListPath);
        }
    }
}