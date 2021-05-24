using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.Tasks
{
    public class MigrateGameFolder
    {
        public static async Task<bool> Execute(AbsolutePath mo2Folder)
        {
            var iniPath = mo2Folder.Combine(Consts.ModOrganizer2Ini);
            if (!iniPath.Exists)
            {
                Utils.Error($"Game folder conversion failed, {Consts.ModOrganizer2Ini} does not exist in {mo2Folder}");
                return false;
            }

            var newGamePath = mo2Folder.Combine(Consts.GameFolderFilesDir);
            newGamePath.CreateDirectory();
            var gameIni = iniPath.LoadIniFile();

            if (!GameRegistry.TryGetByFuzzyName((string)gameIni.General.gameName, out var gameMeta))
            {
                Utils.Error($"Could not locate game for {gameIni.General.gameName}");
                return false;
            }


            var orginGamePath = gameMeta.GameLocation();
            foreach (var file in gameMeta.GameLocation().EnumerateFiles())
            {
                var relPath = file.RelativeTo(orginGamePath);
                var newFile = relPath.RelativeTo(newGamePath);
                if (newFile.Exists)
                {
                    Utils.Log($"Skipping {relPath} it already exists in the target path");
                    continue;
                }

                Utils.Log($"Copying/Linking {relPath}");
                await file.HardLinkIfOversize(newFile);
            }

            Utils.Log("Remapping INI");
            var iniString = await iniPath.ReadAllTextAsync();
            iniString = iniString.Replace((string)orginGamePath, (string)newGamePath);
            iniString = iniString.Replace(((string)orginGamePath).Replace(@"\", @"\\"), ((string)newGamePath).Replace(@"\", @"\\"));
            iniString = iniString.Replace(((string)orginGamePath).Replace(@"\", @"/"), ((string)newGamePath).Replace(@"\", @"/"));
            await iniPath.WriteAllTextAsync(iniString);

            return true;
        }
    }
}
