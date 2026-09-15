namespace Wabbajack.Networking.Steam;

/// <summary>
///     Names for the free companion apps a repair can reach, and the sentence that has to be said before it
///     does.
///     <para>
///         Steam will not so much as describe one of these to an account that holds no licence for it, so
///         reaching its files means taking the free licence first - and taking it puts the app in the user's
///         library, exactly as pressing Install on its store page would. Nothing else about fetching game
///         files touches a Steam account at all: depots are read with licences the user already has, and
///         what comes back is written to the downloads folder. So this is the one thing a repair has to say
///         out loud before the user agrees to it, rather than afterwards.
///     </para>
///     <para>
///         Which apps these are is <c>GameMetaData.SteamToolIDs</c> and not this table: a game with a
///         companion app is the whole condition, and keeping the decision there means a registry entry added
///         later needs nothing here. This only turns an app id into something worth reading, and an id
///         nobody has named still gets said - as the game's app, by number - because the point is to name
///         what is about to be added rather than to guess at it.
///     </para>
/// </summary>
public static class FreeLicenseApps
{
    /// <summary>
    ///     The Creation Kits, by the app id <c>GameRegistry</c> records for each. A name here is a
    ///     convenience, not a gate; see <see cref="Name" />.
    /// </summary>
    private static readonly IReadOnlyDictionary<uint, string> Named = new Dictionary<uint, string>
    {
        [202480] = "the Skyrim Creation Kit",
        [1946180] = "the Skyrim Special Edition Creation Kit",
        [1946160] = "the Fallout 4 Creation Kit",
        [2722710] = "the Starfield Creation Kit"
    };

    /// <summary>
    ///     What to call this app. Falls back to the game's own name and the number, so an app added to
    ///     <c>SteamToolIDs</c> and not to this table is still named rather than silently dropped.
    /// </summary>
    public static string Name(uint appId, string gameName)
    {
        return Named.TryGetValue(appId, out var named) ? named : $"a free {gameName} app (Steam app {appId})";
    }

    /// <summary>
    ///     What to tell the user before a repair that could reach these apps. Empty when it could reach
    ///     none, which is the ordinary answer: a warning shown on every repair is one people learn to scroll
    ///     past by the time it matters.
    ///     <para>
    ///         Deliberately "if one of them turns out to be needed" rather than "this will": whether a
    ///         companion app is actually read is not known until the game's own depots have been searched
    ///         and found wanting, which is long after this is said. Promising more than that would be a lie
    ///         on the great majority of repairs, which are a DLC the game's own depots carry.
    ///     </para>
    /// </summary>
    public static IReadOnlyList<string> Describe(IEnumerable<string> toolNames)
    {
        var tools = toolNames.Distinct().ToArray();
        if (tools.Length == 0) return Array.Empty<string>();

        return new[]
        {
            $"Some of these files ship in {Join(tools)}, which Steam lists separately from the game. If one " +
            "of them turns out to be needed, Wabbajack adds that free app to your Steam library, the way " +
            "pressing Install on its store page does. Nothing is bought, it can be removed again, and " +
            "nothing else here touches your Steam account."
        };
    }

    private static string Join(IReadOnlyList<string> names)
    {
        return names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
        };
    }
}
