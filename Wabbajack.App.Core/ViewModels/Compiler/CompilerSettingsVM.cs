using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack;

public partial class CompilerSettingsVM : ViewModel
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
        ModListCommunity = cs.ModListCommunity;
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
        AutoGenerateReport = cs.AutoGenerateReport;
    }

    [Reactive] public partial bool ModlistIsNSFW { get; set; }
    [Reactive] public partial AbsolutePath Source { get; set; }
    [Reactive] public partial AbsolutePath Downloads { get; set; }
    [Reactive] public partial Game Game { get; set; }
    [Reactive] public partial AbsolutePath OutputFile { get; set; }

    [Reactive] public partial bool AutoGenerateReport { get; set; } = false;

    [Reactive] public partial AbsolutePath ModListImage { get; set; }
    [Reactive] public partial bool UseGamePaths { get; set; }

    [Reactive] public partial bool UseTextureRecompression { get; set; } = false;
    [Reactive] public partial Game[] OtherGames { get; set; } = Array.Empty<Game>();

    [Reactive] public partial TimeSpan MaxVerificationTime { get; set; } = TimeSpan.FromMinutes(1);
    [Reactive] public partial string ModListName { get; set; } = "";
    [Reactive] public partial string ModListAuthor { get; set; } = "";
    [Reactive] public partial string ModListDescription { get; set; } = "";
    [Reactive] public partial string ModListReadme { get; set; } = "";
    [Reactive] public partial string ModListWebsite { get; set; } = "";
    [Reactive] public partial string ModListCommunity { get; set; } = "";
    [Reactive] public partial string ModlistVersion { get; set; } = "";
    [Reactive] public partial string MachineUrl { get; set; } = "";

    /// <summary>
    /// The main (default) profile
    /// </summary>
    [Reactive] public partial string Profile { get; set; } = "";

    /// <summary>
    /// Secondary profiles to include in the modlist
    /// </summary>
    [Reactive] public partial string[] AdditionalProfiles { get; set; } = Array.Empty<string>();


    /// <summary>
    /// All profiles to be added to the compiled modlist
    /// </summary>
    public IEnumerable<string> AllProfiles => AdditionalProfiles.Append(Profile);

    public bool IsMO2Modlist => AllProfiles.Any(p => !string.IsNullOrWhiteSpace(p));



    /// <summary>
    ///     This file, or files in these folders, are automatically included if they don't match
    ///     any other step
    /// </summary>
    [Reactive] public partial HashSet<RelativePath> NoMatchInclude { get; set; } = new();

    /// <summary>
    ///     These files are inlined into the modlist
    /// </summary>
    [Reactive] public partial HashSet<RelativePath> Include { get; set; } = new();

    /// <summary>
    ///     These files are ignored when compiling the modlist
    /// </summary>
    [Reactive] public partial HashSet<RelativePath> Ignore { get; set; } = new();

    [Reactive] public partial HashSet<RelativePath> AlwaysEnabled { get; set; } = new();
    [Reactive] public partial string Version { get; set; }
    [Reactive] public partial string Description { get; set; }

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
            ModListCommunity = ModListCommunity,
            ModlistVersion = System.Version.Parse(ModlistVersion),
            MachineUrl = MachineUrl,
            Profile = Profile,
            AdditionalProfiles = AdditionalProfiles,
            NoMatchInclude = NoMatchInclude.ToArray(),
            Include = Include.ToArray(),
            Ignore = Ignore.ToArray(),
            AlwaysEnabled = AlwaysEnabled.ToArray(),
            Version = System.Version.Parse(Version),
            Description = Description,
            AutoGenerateReport = AutoGenerateReport
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
