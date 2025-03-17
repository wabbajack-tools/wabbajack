using System;
using System.Runtime.InteropServices.JavaScript;
using Wabbajack.Paths;

namespace Wabbajack.DTOs;

public class GameMetaData
{
    public Game Game { get; internal init; }

    public bool IsGenericMO2Plugin { get; internal init; }

    public string? MO2ArchiveName { get; internal init; }

    public string? NexusName { get; internal init; }

    // Nexus DB id for the game, used in some specific situations
    public long NexusGameId { get; internal init; }
    public string? MO2Name { get; internal init; }

    // to get steam ids: https://steamdb.info
    public int[] SteamIDs { get; internal init; } = Array.Empty<int>();

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