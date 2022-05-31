using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Compiler;

public class CompilerSettings
{
    public bool ModlistIsNSFW { get; set; }
    public AbsolutePath Source { get; set; }
    public AbsolutePath Downloads { get; set; }
    public Game Game { get; set; }
    public AbsolutePath OutputFile { get; set; }

    public AbsolutePath ModListImage { get; set; }
    public bool UseGamePaths { get; set; }

    public bool UseTextureRecompression { get; set; } = false;
    public Game[] OtherGames { get; set; } = Array.Empty<Game>();

    public TimeSpan MaxVerificationTime { get; set; } = TimeSpan.FromMinutes(1);
    public string ModListName { get; set; } = "";
    public string ModListAuthor { get; set; } = "";
    public string ModListDescription { get; set; } = "";
    public string ModlistReadme { get; set; } = "";
    public Uri? ModListWebsite { get; set; }
    public Version ModlistVersion { get; set; } = Version.Parse("0.0.1.0");
    
    /// <summary>
    /// The main (default) profile
    /// </summary>
    public string Profile { get; set; } = "";

    /// <summary>
    /// Secondary profiles to include in the modlist
    /// </summary>
    public string[] AdditionalProfiles { get; set; } = Array.Empty<string>();
    
    
    /// <summary>
    /// All profiles to be added to the compiled modlist
    /// </summary>
    [JsonIgnore]
    public IEnumerable<string> AllProfiles => AdditionalProfiles.Append(Profile);



    /// <summary>
    ///     This file, or files in these folders, are automatically included if they don't match
    ///     any other step
    /// </summary>
    public RelativePath[] NoMatchInclude { get; set; } = Array.Empty<RelativePath>();

    /// <summary>
    ///     These files are inlined into the modlist
    /// </summary>
    public RelativePath[] Include { get; set; } = Array.Empty<RelativePath>();

    public RelativePath[] AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();

}