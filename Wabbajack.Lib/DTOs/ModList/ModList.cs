using System;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs;

[JsonName("ModList")]
public class ModList
{
    /// <summary>
    ///     Archives required by this modlist
    /// </summary>
    public Archive[] Archives { get; set; } = Array.Empty<Archive>();

    /// <summary>
    ///     Author of the ModList
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    ///     Description of the ModList
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Install directives
    /// </summary>
    public Directive[] Directives { get; set; } = Array.Empty<Directive>();

    /// <summary>
    ///     The game variant to which this game applies
    /// </summary>
    public Game GameType { get; set; }

    public Game[] OtherGames { get; set; } = Array.Empty<Game>();

    /// <summary>
    ///     Hash of the banner-image
    /// </summary>
    public RelativePath Image { get; set; }

    /// <summary>
    ///     Name of the ModList
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     URL to the readme
    /// </summary>
    public string Readme { get; set; } = string.Empty;

    /// <summary>
    ///     The build version of Wabbajack used when compiling the Modlist
    /// </summary>
    public Version? WabbajackVersion { get; set; }

    /// <summary>
    ///     URL to the modlist website
    /// </summary>
    public string Website { get; set; } = string.Empty;

    /// <summary>
    ///     URL to the modlist community
    /// </summary>
    public string Community { get; set; } = string.Empty;

    /// <summary>
    ///     Current Version of the Modlist
    /// </summary>
    public Version Version { get; set; } = new(1, 0, 0, 0);

    /// <summary>
    ///     Whether the Modlist is NSFW or not
    /// </summary>
    public bool IsNSFW { get; set; }

    public ModList Strip()
    {
        return new ModList
        {
            Author = Author,
            Description = Description,
            GameType = GameType,
            Name = Name,
            Readme = Readme,
            WabbajackVersion = WabbajackVersion,
            Website = Website,
            Version = Version,
            IsNSFW = IsNSFW,
            Community = Community
        };
    }
}