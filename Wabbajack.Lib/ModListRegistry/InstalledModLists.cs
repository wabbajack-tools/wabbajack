using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.ModListRegistry
{
    public class InstalledModLists
    {
        public static AbsolutePath InstalledModlistsLocation = Consts.LocalAppDataPath.Combine("installed_modlists.json");
        private static AsyncLock _lock = new();

        public static async Task AddModListInstall(ModlistMetadata? metadata, ModList modList, AbsolutePath installPath,
            AbsolutePath downloadPath, AbsolutePath wabbjackPath)
        {
            modList = modList.Clone();
            modList.Directives = new List<Directive>();
            modList.Archives = new List<Archive>();

            var newRecord = new ModListInstall()
            {
                Metadata = metadata,
                ModList = modList,
                InstallationPath = installPath,
                DownloadPath = downloadPath,
                WabbajackPath = wabbjackPath,
            };
            await UpsertInstall(newRecord);
        }

        public static async Task UpsertInstall(ModListInstall newRecord)
        {
            using var _ = await _lock.WaitAsync();
            Dictionary<AbsolutePath, ModListInstall> oldRecords = new();
            if (InstalledModlistsLocation.Exists)
                oldRecords = await InstalledModlistsLocation.FromJsonAsync<Dictionary<AbsolutePath, ModListInstall>>();
            
            oldRecords[newRecord.InstallationPath] = newRecord;
            
            CleanEntries(oldRecords);
            
            await oldRecords.ToJsonAsync(InstalledModlistsLocation);
        }

        private static void CleanEntries(Dictionary<AbsolutePath, ModListInstall> oldRecords)
        {
            oldRecords.Keys
                .Where(k => !k.IsDirectory)
                .ToArray()
                .Do(k => oldRecords.Remove(k));
        }
    }

    [JsonName("ModListInstall")]
    public class ModListInstall
    {
        public ModlistMetadata? Metadata { get; set; }
        public ModList ModList { get; set; } = new();
        public AbsolutePath InstallationPath { get; set; }
        public AbsolutePath DownloadPath { get; set; }
        public AbsolutePath WabbajackPath { get; set; }
        public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    }
}
