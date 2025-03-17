using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack;

public class CompilerSettingsVM : ViewModel
{
    public CompilerSettingsVM() { }
    public CompilerSettingsVM(CompilerSettings cs)
    {
        ModlistIsNSFW = cs.ModlistIsNSFW;
        Source = cs.Source;
        Downloads = cs.Downloads;
        Game = cs.Game;
        OutputFile = cs.OutputFile;
        ModListImage = cs.ModListImage;
        UseGamePaths = cs.UseGamePaths;
        UseTextureRecompression = cs.UseTextureRecompression;
        OtherGames = cs.OtherGames;
        MaxVerificationTime = cs.MaxVerificationTime;
        ModListName = cs.ModListName;
        ModListAuthor = cs.ModListAuthor;
        ModListDescription = cs.ModListDescription;
        ModListReadme = cs.ModListReadme;
        ModListWebsite = cs.ModListWebsite;
        ModlistVersion = cs.ModlistVersion?.ToString() ?? "";
        MachineUrl = cs.MachineUrl;
        Profile = cs.Profile;
        AdditionalProfiles = cs.AdditionalProfiles;
        NoMatchInclude = cs.NoMatchInclude.ToHashSet();
        Include = cs.Include.ToHashSet();
        Ignore = cs.Ignore.ToHashSet();
        AlwaysEnabled = cs.AlwaysEnabled.ToHashSet();
        Version = cs.Version?.ToString() ?? "";
        Description = cs.Description;
    }

    [Reactive] public bool ModlistIsNSFW { get; set; }
    [Reactive] public AbsolutePath Source { get; set; }
    [Reactive] public AbsolutePath Downloads { get; set; }
    [Reactive] public Game Game { get; set; }
    [Reactive] public AbsolutePath OutputFile { get; set; }

    [Reactive] public AbsolutePath ModListImage { get; set; }
    [Reactive] public bool UseGamePaths { get; set; }

    [Reactive] public bool UseTextureRecompression { get; set; } = false;
    [Reactive] public Game[] OtherGames { get; set; } = Array.Empty<Game>();

    [Reactive] public TimeSpan MaxVerificationTime { get; set; } = TimeSpan.FromMinutes(1);
    [Reactive] public string ModListName { get; set; } = "";
    [Reactive] public string ModListAuthor { get; set; } = "";
    [Reactive] public string ModListDescription { get; set; } = "";
    [Reactive] public string ModListReadme { get; set; } = "";
    [Reactive] public Uri? ModListWebsite { get; set; }
    [Reactive] public string ModlistVersion { get; set; } = "";
    [Reactive] public string MachineUrl { get; set; } = "";

    /// <summary>
    /// The main (default) profile
    /// </summary>
    [Reactive] public string Profile { get; set; } = "";

    /// <summary>
    /// Secondary profiles to include in the modlist
    /// </summary>
    [Reactive] public string[] AdditionalProfiles { get; set; } = Array.Empty<string>();


    /// <summary>
    /// All profiles to be added to the compiled modlist
    /// </summary>
    public IEnumerable<string> AllProfiles => AdditionalProfiles.Append(Profile);

    public bool IsMO2Modlist => AllProfiles.Any(p => !string.IsNullOrWhiteSpace(p));



    /// <summary>
    ///     This file, or files in these folders, are automatically included if they don't match
    ///     any other step
    /// </summary>
    [Reactive] public HashSet<RelativePath> NoMatchInclude { get; set; } = new();

    /// <summary>
    ///     These files are inlined into the modlist
    /// </summary>
    [Reactive] public HashSet<RelativePath> Include { get; set; } = new();

    /// <summary>
    ///     These files are ignored when compiling the modlist
    /// </summary>
    [Reactive] public HashSet<RelativePath> Ignore { get; set; } = new();

    [Reactive] public HashSet<RelativePath> AlwaysEnabled { get; set; } = new();
    [Reactive] public string Version { get; set; }
    [Reactive] public string Description { get; set; }

    public CompilerSettings ToCompilerSettings()
    {
        return new CompilerSettings()
        {
            ModlistIsNSFW = ModlistIsNSFW,
            Source = Source,
            Downloads = Downloads,
            Game = Game,
            OutputFile = OutputFile,
            ModListImage = ModListImage,
            UseGamePaths = UseGamePaths,
            UseTextureRecompression = UseTextureRecompression,
            OtherGames = OtherGames,
            MaxVerificationTime = MaxVerificationTime,
            ModListName = ModListName,
            ModListAuthor = ModListAuthor,
            ModListDescription = ModListDescription,
            ModListReadme = ModListReadme,
            ModListWebsite = ModListWebsite,
            ModlistVersion = System.Version.Parse(ModlistVersion),
            MachineUrl = MachineUrl,
            Profile = Profile,
            AdditionalProfiles = AdditionalProfiles,
            NoMatchInclude = NoMatchInclude.ToArray(),
            Include = Include.ToArray(),
            Ignore = Ignore.ToArray(),
            AlwaysEnabled = AlwaysEnabled.ToArray(),
            Version = System.Version.Parse(Version),
            Description = Description
        };
    }
    public AbsolutePath CompilerSettingsPath
    {
        get
        {
            if (Source == default || string.IsNullOrEmpty(Profile)) return default;
            return Source.Combine(ModListName).WithExtension(Ext.CompilerSettings);
        }
    }
    public AbsolutePath ProfilePath
    {
        get
        {
            if (Source == default || string.IsNullOrEmpty(Profile)) return default;
            return Source.Combine("profiles").Combine(Profile).Combine("modlist").WithExtension(Ext.Txt);
        }
    }
}
