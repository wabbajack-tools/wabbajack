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

    public async Task<CompilerSettings?> InferFromRootPath(AbsolutePath rootPath)
    {
        var mo2File = rootPath.Combine(Consts.MO2IniName).LoadIniFile();
        var profile = mo2File["General"]["selected_profile"].FromMO2Ini();

        return await InferModListFromLocation(rootPath.Combine(Consts.MO2Profiles, profile, Consts.ModListTxt));
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
                cs.Profile = selectedProfile;

                cs.OutputFile = cs.Source.Parent.Combine(selectedProfile).WithExtension(Ext.Wabbajack);
                
                var settings = iniData["Settings"];
                cs.Downloads = settings["download_directory"].FromMO2Ini().ToAbsolutePath();

                if (cs.Downloads == default)
                    cs.Downloads = cs.Source.Combine("downloads");

                cs.NoMatchInclude = Array.Empty<RelativePath>();
                cs.Include = Array.Empty<RelativePath>();
                foreach (var file in mo2Folder.EnumerateFiles())
                {
                    if (file.FileName == Consts.WABBAJACK_NOMATCH_INCLUDE_FILES)
                        cs.NoMatchInclude = cs.NoMatchInclude.Add(file.Parent.RelativeTo(mo2Folder));

                    if (file.FileName.WithoutExtension().ToString() == Consts.WABBAJACK_INCLUDE)
                        cs.Include = cs.Include.Add(file.Parent.RelativeTo(mo2Folder));
                    
                    if (file.FileName.WithoutExtension().ToString() == Consts.WABBAJACK_NOMATCH_INCLUDE)
                        cs.NoMatchInclude = cs.NoMatchInclude.Add(file.Parent.RelativeTo(mo2Folder));
                }

                _logger.LogInformation("Finding Always Enabled mods");
                cs.AlwaysEnabled = Array.Empty<RelativePath>();
                
                // Find mod tags
                foreach (var modFolder in mo2Folder.Combine("mods").EnumerateDirectories())
                {
                    var iniFile = modFolder.Combine("meta.ini");
                    if (!iniFile.FileExists()) continue;

                    var data = iniFile.LoadIniFile();
                    var generalModData = data["General"];
                    if ((generalModData["notes"]?.Contains(Consts.WABBAJACK_ALWAYS_ENABLE) ?? false) ||
                        (generalModData["comments"]?.Contains(Consts.WABBAJACK_ALWAYS_ENABLE) ?? false))
                        cs.AlwaysEnabled = cs.AlwaysEnabled.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                    
                    if ((generalModData["notes"]?.Contains(Consts.WABBAJACK_NOMATCH_INCLUDE) ?? false) ||
                        (generalModData["comments"]?.Contains(Consts.WABBAJACK_NOMATCH_INCLUDE) ?? false))
                        cs.NoMatchInclude = cs.NoMatchInclude.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                    
                    if ((generalModData["notes"]?.Contains(Consts.WABBAJACK_INCLUDE) ?? false) ||
                        (generalModData["comments"]?.Contains(Consts.WABBAJACK_INCLUDE) ?? false))
                        cs.Include = cs.Include.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                }

                _logger.LogInformation("Finding other profiles");
                var otherProfilesFile = settingsFile.Parent.Combine("otherprofiles.txt");
                if (otherProfilesFile.FileExists())
                {
                    cs.AdditionalProfiles = await otherProfilesFile.ReadAllLinesAsync().ToArray();
                }

                cs.OutputFile = cs.Source.Parent.Combine(cs.Profile).WithExtension(Ext.Wabbajack);
            }

            return cs;
        }

        return null;
    }
}