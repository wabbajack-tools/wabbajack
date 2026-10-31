using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler;

/// <summary>
///     Makes a .compiler_settings file from another machine usable on this one.
///
///     The app saves a list's settings into its MO2 folder, and the compiler packs that file into the list, so
///     every install of a list carries its author's settings. Loading one as it is points the compiler at the
///     author's folders. A settings file always sits in the MO2 folder it describes, so that folder is the
///     source; downloads and output that do not exist here are found the way a new list's are; an image that
///     is not here is dropped. When downloads or output had to move, the file came from another machine, and
///     its machine URL and publish setting, which name the author's gallery entry, are cleared. A user's own
///     file, or one whose folder was only moved, keeps them.
/// </summary>
public static class ReusedCompilerSettings
{
    /// <param name="settings">Changed in place.</param>
    /// <param name="settingsFile">Where the settings were loaded from.</param>
    /// <param name="inferred">What a new list in that folder would get, if its profile could be read.</param>
    /// <returns>What was changed, for the log.</returns>
    public static IReadOnlyList<string> Repoint(CompilerSettings settings, AbsolutePath settingsFile,
        CompilerSettings? inferred)
    {
        var changed = new List<string>();
        var folder = settingsFile.Parent;

        if (settings.Source != folder)
        {
            settings.Source = folder;
            changed.Add("source");
        }

        var fromAnotherMachine = false;
        if (settings.Downloads == default || !settings.Downloads.DirectoryExists())
        {
            settings.Downloads = inferred?.Downloads ?? folder.Combine("downloads");
            changed.Add("downloads");
            fromAnotherMachine = true;
        }

        if (settings.OutputFile == default || !settings.OutputFile.Parent.DirectoryExists())
        {
            var name = string.IsNullOrWhiteSpace(settings.ModListName) ? folder.FileName.ToString() : settings.ModListName;
            settings.OutputFile = folder.Parent.Combine(name).WithExtension(Ext.Wabbajack);
            changed.Add("output file");
            fromAnotherMachine = true;
        }

        if (settings.ModListImage != default && !settings.ModListImage.FileExists())
        {
            settings.ModListImage = default;
            changed.Add("image");
        }

        if (fromAnotherMachine && (settings.PublishUpdate || !string.IsNullOrEmpty(settings.MachineUrl)))
        {
            settings.PublishUpdate = false;
            settings.MachineUrl = "";
            changed.Add("machine URL");
        }

        return changed;
    }
}
