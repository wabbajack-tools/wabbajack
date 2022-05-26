using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler;

/// <summary>
/// Given a modlist.txt file, infer as much of CompilerSettings as possible
/// </summary>
public class CompilerSettingsInferencer
{
    private readonly ILogger<CompilerSettingsInferencer> _logger;

    public CompilerSettingsInferencer(ILogger<CompilerSettingsInferencer> logger)
    {
        _logger = logger;

    }
    
    public async Task<CompilerSettings?> InferModListFromLocation(AbsolutePath settingsFile)
    {
        var cs = new CompilerSettings();

        if (settingsFile.FileName == "modlist.txt".ToRelativePath() && settingsFile.Depth > 3)
        {
            _logger.LogInformation("Inferencing basic settings");
            var mo2Folder = settingsFile.Parent.Parent.Parent;
            var mo2Ini = mo2Folder.Combine(Consts.MO2IniName);
            if (mo2Ini.FileExists())
            {
                var iniData = mo2Ini.LoadIniFile();

                var general = iniData["General"];

                cs.Game = GameRegistry.GetByFuzzyName(general["gameName"].FromMO2Ini()).Game;
                cs.Source = mo2Folder;

                var selectedProfile = general["selected_profile"].FromMO2Ini();
                //cs.GamePath = general["gamePath"].FromMO2Ini().ToAbsolutePath();
                cs.ModListName = selectedProfile;
                
                cs.OutputFile = cs.Source.Parent;
                
                var settings = iniData["Settings"];
                cs.Downloads = settings["download_directory"].FromMO2Ini().ToAbsolutePath();

                if (cs.Downloads == default)
                    cs.Downloads = cs.Source.Combine("downloads");

                cs.NoMatchInclude = Array.Empty<RelativePath>();
                foreach (var file in mo2Folder.EnumerateFiles())
                {
                    if (file.FileName == Consts.WABBAJACK_NOMATCH_INCLUDE_FILES)
                        cs.NoMatchInclude = cs.NoMatchInclude.Add(file.Parent.RelativeTo(mo2Folder));
                }

                _logger.LogInformation("Finding Always Enabled mods");
                cs.AlwaysEnabled = Array.Empty<RelativePath>();
                // Find Always Enabled mods
                foreach (var modFolder in mo2Folder.Combine("mods").EnumerateDirectories())
                {
                    var iniFile = modFolder.Combine("meta.ini");
                    if (!iniFile.FileExists()) continue;

                    var data = iniFile.LoadIniFile();
                    var generalModData = data["General"];
                    if ((generalModData["notes"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false) ||
                        (generalModData["comments"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false))
                        cs.AlwaysEnabled = cs.AlwaysEnabled.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                }

                _logger.LogInformation("Finding other profiles");
                var otherProfilesFile = settingsFile.Parent.Combine("otherprofiles.txt");
                if (otherProfilesFile.FileExists())
                {
                    cs.OtherProfiles = await otherProfilesFile.ReadAllLinesAsync().ToArray();
                }
            }

            return cs;
        }

        return null;
    }
}