using System;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using Wabbajack.Paths;

namespace Wabbajack.DTOs;

public class GameMetaData
{
    public Game Game { get; internal init; }

    public bool IsGenericMO2Plugin { get; internal init; }

    public string? MO2ArchiveName { get; internal init; }

    public string? NexusName { get; internal init; }

    /// <summary>
    ///     The game's address on Nexus Mods: the segment in <c>https://www.nexusmods.com/{domain}/mods/{id}</c>.
    ///     <see cref="NexusName" /> where the registry records one, and where it does not - Terraria and
    ///     Karryn's Prison are the only two - the game's own name stripped to letters and digits and
    ///     lowercased, which is how Nexus spells both of those. Never empty, so nothing builds a
    ///     <c>//mods/</c> URL with no domain in it, which resolves to nothing.
    /// </summary>
    public string NexusDomain => string.IsNullOrWhiteSpace(NexusName)
        ? new string(Game.ToString().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()
        : NexusName;

    // Nexus DB id for the game, used in some specific situations
    public long NexusGameId { get; internal init; }
    public string? MO2Name { get; internal init; }

    // to get steam ids: https://steamdb.info
    public int[] SteamIDs { get; internal init; } = Array.Empty<int>();

    /// <summary>
    ///     Steam apps that are not the game but install their files into the game's own folder. The Creation
    ///     Kit is what this exists for: Steam gives it its own app id and its own depots, while its
    ///     <c>installdir</c> names the game, so <c>CreationKit.exe</c> and <c>Data\Scripts.zip</c> land
    ///     beside <c>SkyrimSE.exe</c> and a modlist records them as the game's own files, because that is
    ///     where they are. Nothing but the depot they have to come from distinguishes them.
    ///     <para>
    ///         These are never used to locate an install - the tool has no executable of its own worth
    ///         finding and shares the game's directory - so they stay out of <see cref="SteamIDs" />, which
    ///         <c>GameLocator</c> walks. They only answer "where else could this file be fetched from".
    ///     </para>
    /// </summary>
    public int[] SteamToolIDs { get; internal init; } = Array.Empty<int>();

    // to get gog ids: https://www.gogdb.org
    public long[] GOGIDs { get; internal init; } = Array.Empty<long>();

    // to get these ids, split the numbers from the letters in file names found in
    // C:\ProgramData\Origin\LocalContent\{game name)\*.mfst
    // So for DA:O this is "DR208591800.mfst" -> "DR:208591800"
    // EAPlay games may have @subscription appended to the file name
    public string[] OriginIDs { get; set; } = Array.Empty<string>();

    public string[] EADesktopIDs { get; set; } = Array.Empty<string>();

    public string[] EpicGameStoreIDs { get; internal init; } = Array.Empty<string>();

    // to get BethNet IDs: check the registry
    public int BethNetID { get; internal init; }

    //for BethNet games only!
    public string RegString { get; internal init; } = string.Empty;

    // file to check if the game is present, useful when steamIds and gogIds dont help
    public RelativePath[] RequiredFiles { get; internal init; } = Array.Empty<RelativePath>();

    public RelativePath? MainExecutable { get; internal init; }

    // Games that this game are commonly confused with, for example Skyrim SE vs Skyrim LE
    public Game[] CommonlyConfusedWith { get; set; } = Array.Empty<Game>();

    /// <summary>
    ///     Other games this game can pull source files from (if the game is installed on the user's machine)
    /// </summary>
    public Game[] CanSourceFrom { get; set; } = Array.Empty<Game>();

    public string HumanFriendlyGameName => Game.GetDescription();
    /// <summary>
    /// URI to an ICO / PNG, preferred size 32x32
    /// </summary>
    public string IconSource { get; set; } = @"Resources/Icons/wabbajack.ico";
}