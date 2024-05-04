using Microsoft.Web.WebView2.Core;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
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
        ModlistVersion = cs.ModlistVersion;
        PublishUpdate = cs.PublishUpdate;
        MachineUrl = cs.MachineUrl;
        Profile = cs.Profile;
        AdditionalProfiles = cs.AdditionalProfiles;
        NoMatchInclude = cs.NoMatchInclude;
        Include = cs.Include;
        Ignore = cs.Ignore;
        AlwaysEnabled = cs.AlwaysEnabled;
        Version = cs.Version;
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
    [Reactive] public Version ModlistVersion { get; set; } = Version.Parse("0.0.1.0");
    [Reactive] public bool PublishUpdate { get; set; } = false;
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
    [Reactive] public RelativePath[] NoMatchInclude { get; set; } = Array.Empty<RelativePath>();

    /// <summary>
    ///     These files are inlined into the modlist
    /// </summary>
    [Reactive] public RelativePath[] Include { get; set; } = Array.Empty<RelativePath>();

    /// <summary>
    ///     These files are ignored when compiling the modlist
    /// </summary>
    [Reactive] public RelativePath[] Ignore { get; set; } = Array.Empty<RelativePath>();

    [Reactive] public RelativePath[] AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();
    [Reactive] public Version Version { get; set; }
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
            ModlistVersion = ModlistVersion,
            PublishUpdate = PublishUpdate,
            MachineUrl = MachineUrl,
            Profile = Profile,
            AdditionalProfiles = AdditionalProfiles,
            NoMatchInclude = NoMatchInclude,
            Include = Include,
            Ignore = Ignore,
            AlwaysEnabled = AlwaysEnabled,
            Version = Version,
            Description = Description
        };
    }
    public AbsolutePath CompilerSettingsPath => Source.Combine(ModListName).WithExtension(Ext.CompilerSettings);
    public AbsolutePath ProfilePath => Source.Combine("profiles").Combine(Profile).Combine("modlist").WithExtension(Ext.Txt);
}
