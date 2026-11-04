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

    /// <summary>
    ///     Points settings loaded from <paramref name="settingsFile" /> at this machine; see
    ///     <see cref="ReusedCompilerSettings" />. Returns what was changed.
    /// </summary>
    public async Task<IReadOnlyList<string>> RepointForThisMachine(CompilerSettings settings, AbsolutePath settingsFile)
    {
        var modlistTxt = settingsFile.Parent.Combine(Consts.MO2Profiles, settings.Profile, Consts.ModListTxt);
        var inferred = !string.IsNullOrEmpty(settings.Profile) && modlistTxt.FileExists()
            ? await InferModListFromLocation(modlistTxt)
            : null;

        var changed = ReusedCompilerSettings.Repoint(settings, settingsFile, inferred);
        if (changed.Count > 0)
            _logger.LogInformation("Settings in {File} were made for another folder; changed {Changed} to suit this one",
                settingsFile, string.Join(", ", changed));
        return changed;
    }

    /// <summary>
    ///     A folder MO2 wrote, as this machine names it. Under Wine or Proton a Z: path is a Unix one; any other
    ///     drive letter belongs to whichever prefix MO2 ran in, which is looked for. Default when none has it.
    /// </summary>
    private AbsolutePath FromProgramPath(string? written)
    {
        var (path, prefix) = WinePrefixes.FromWindowsProgramPath(written);
        if (prefix != default)
            _logger.LogInformation("{Written} is {Path}, through the Wine prefix {Prefix}", written, path, prefix);
        else if (path == default && !string.IsNullOrWhiteSpace(written))
            _logger.LogWarning("{Written} is on a Windows drive none of the Wine prefixes found here has", written);
        return path;
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

                // The profile is the one whose modlist.txt was picked, not whichever MO2 last had selected.
                var selectedProfile = settingsFile.Parent.FileName.ToString();
                //cs.GamePath = general["gamePath"].FromMO2Ini().ToAbsolutePath();
                cs.ModListName = selectedProfile;
                cs.Profile = selectedProfile;

                cs.OutputFile = cs.Source.Parent.Combine(cs.ModListName).WithExtension(Ext.Wabbajack);
                
                var settings = iniData["Settings"];
                cs.Downloads = FromProgramPath(settings["download_directory"].FromMO2Ini());

                if (cs.Downloads == default)
                    cs.Downloads = cs.Source.Combine("downloads");

                cs.NoMatchInclude = Array.Empty<RelativePath>();
                cs.Include = Array.Empty<RelativePath>();
                foreach (var file in mo2Folder.EnumerateFiles())
                {
                    if (MatchesVariants(file, Consts.WABBAJACK_INCLUDE))
                        cs.Include = cs.Include.Add(file.Parent.RelativeTo(mo2Folder));
                    
                    if (MatchesVariants(file, Consts.WABBAJACK_NOMATCH_INCLUDE))
                        cs.NoMatchInclude = cs.NoMatchInclude.Add(file.Parent.RelativeTo(mo2Folder));
                    if (MatchesVariants(file, Consts.WABBAJACK_NOMATCH_INCLUDE, true))
                    {
                        cs.NoMatchInclude = cs.NoMatchInclude.Add(file.RelativeTo(mo2Folder));
                        var taggedFiles = await file.ReadAllLinesAsync().ToArray();
                        foreach (var taggedFile in taggedFiles)
                        {
                            // A name typed into the tag file need not match the file's case on disk.
                            var noMatchIncludeAsset = string.IsNullOrWhiteSpace(taggedFile)
                                ? (AbsolutePath) file.ToString().Replace(file.FileName.ToString(), taggedFile)
                                : file.Parent.FindIgnoringCase(taggedFile.ToRelativePath());
                            if (noMatchIncludeAsset.DirectoryExists())
                                cs.NoMatchInclude = cs.NoMatchInclude.Add(noMatchIncludeAsset
                                .RelativeTo(mo2Folder));
                            if (noMatchIncludeAsset.FileExists())
                                cs.Include = cs.Include.Add(noMatchIncludeAsset
                                    .RelativeTo(mo2Folder)); 
                        }
                    }

                    if (MatchesVariants(file, Consts.WABBAJACK_IGNORE))
                        cs.Ignore = cs.Ignore.Add(file.Parent.RelativeTo(mo2Folder));
                    if (MatchesVariants(file, Consts.WABBAJACK_IGNORE, true))
                    {
                        cs.Ignore = cs.Ignore.Add(file.RelativeTo(mo2Folder));
                        var taggedFiles = await file.ReadAllLinesAsync().ToArray();
                        foreach (var taggedFile in taggedFiles)
                        {
                            cs.Ignore = cs.Ignore.Add(
                                ((AbsolutePath) file.ToString().Replace(file.FileName.ToString(), taggedFile))
                                .RelativeTo(mo2Folder));
                        }
                    }
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
                    
                    if ((generalModData["notes"]?.Contains(Consts.WABBAJACK_IGNORE) ?? false) ||
                        (generalModData["comments"]?.Contains(Consts.WABBAJACK_IGNORE) ?? false) ||
                        (generalModData["notes"]?.Contains(Consts.WABBAJACK_ALWAYS_DISABLE) ?? false) ||
                        (generalModData["comments"]?.Contains(Consts.WABBAJACK_ALWAYS_DISABLE) ?? false))
                        cs.Ignore = cs.Ignore.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                }

                _logger.LogInformation("Finding other profiles");
                var otherProfilesFile = settingsFile.Parent.Combine("otherprofiles.txt");
                if (otherProfilesFile.FileExists())
                {
                    cs.AdditionalProfiles = await otherProfilesFile.ReadAllLinesAsync().ToArray();
                }
            }

            return cs;
        }

        return null;
    }

    private static bool MatchesVariants(AbsolutePath file, string baseVariant, bool readTxtFileMode = false)
    {
        var fileNameString = file.FileName.ToString();
        if (!readTxtFileMode)
        {
            return fileNameString == baseVariant;
        }
        return fileNameString == baseVariant + "_FILES.txt" || fileNameString == baseVariant + "_FILES.TXT";
    }
}