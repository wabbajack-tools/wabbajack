namespace Wabbajack.Networking.Steam;

/// <summary>
///     Steam apps that are free but not licence-free, and what fetching from one costs the user.
///     <para>
///         Steam will not so much as describe one of these to an account that holds no licence for it, so
///         reaching its files means taking the free licence first - and taking it puts the app in the user's
///         library, exactly as installing it from its store page would. Nothing else about fetching game
///         files touches a Steam account at all: depots are read with licences the user already has, and
///         what comes back is written to the downloads folder. So this is the one thing a repair has to say
///         out loud before the user agrees to it, rather than afterwards.
///     </para>
///     <para>
///         Keyed by app id rather than by <c>Game</c>, because the app is what actually decides. A game
///         registry entry added for one of these tools lights this up on its own rather than needing a
///         second list kept in step with the first.
///     </para>
/// </summary>
public static class FreeLicenseApps
{
    /// <summary>
    ///     1946180 is Skyrim Special Edition's Creation Kit. The other games' kits are their own apps and
    ///     belong here as their ids are established.
    /// </summary>
    private static readonly IReadOnlyDictionary<uint, string> Named = new Dictionary<uint, string>
    {
        [1946180] = "the Skyrim Special Edition Creation Kit"
    };

    /// <summary>Whether reaching this app means taking a free licence for it.</summary>
    public static bool NeedsFreeLicense(uint appId)
    {
        return Named.ContainsKey(appId);
    }

    /// <summary>
    ///     What to tell the user before fetching anything out of these apps. Empty when none of them is one
    ///     of these, which is the ordinary answer: a warning shown on every repair is a warning nobody reads
    ///     by the time it matters.
    /// </summary>
    public static IReadOnlyList<string> Describe(IEnumerable<uint> appIds)
    {
        var tools = appIds
            .Distinct()
            .Where(Named.ContainsKey)
            .Select(id => Named[id])
            .Distinct()
            .ToArray();

        if (tools.Length == 0) return Array.Empty<string>();

        return new[]
        {
            $"This adds {Join(tools)} to your Steam library, the same as installing it from its store page: " +
            "Steam will not hand over a free tool's files until the account has a licence for it. Nothing " +
            "else here touches your Steam account."
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
