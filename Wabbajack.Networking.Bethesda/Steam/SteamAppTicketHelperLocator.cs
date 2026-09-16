using System;
using System.Collections.Generic;
using System.IO;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>What to run, and what to put in front of the verb's own arguments.</summary>
public record SteamAppTicketHelperCommand(string FileName, IReadOnlyList<string> LeadingArguments)
{
    public IReadOnlyList<string> WithArguments(IEnumerable<string> arguments)
    {
        var all = new List<string>(LeadingArguments);
        all.AddRange(arguments);
        return all;
    }
}

/// <summary>
///     Finds the Wabbajack CLI, which is what hosts the <c>steam-app-ticket</c> verb.
///     <para>
///         Three layouts have to work. The CLI run by hand is already the right process, so it re-runs itself.
///         A release puts the CLI in a <c>cli</c> folder beside the app. A development build has the CLI's
///         output next to whatever referenced it. The <c>.dll</c> with <c>dotnet</c> in front is the last
///         resort, for a framework-dependent layout with no apphost.
///     </para>
/// </summary>
public static class SteamAppTicketHelperLocator
{
    /// <summary>The CLI's assembly name, which is also its apphost's name.</summary>
    public const string HelperName = "wabbajack-cli";

    /// <summary>Set this to point at a CLI somewhere else entirely; mostly a way in for tests and odd layouts.</summary>
    public const string OverrideVariable = "WABBAJACK_CLI";

    public static string ExecutableName =>
        OperatingSystem.IsWindows() ? HelperName + ".exe" : HelperName;

    /// <summary>
    ///     Works out what to run, or null when no CLI can be found.
    /// </summary>
    /// <param name="baseDirectory">Where the calling process's own assemblies live.</param>
    /// <param name="processPath">The calling process's executable, when it has one.</param>
    /// <param name="exists">Whether a file is there; a parameter so this is testable without a build output.</param>
    /// <param name="overridePath">The value of <see cref="OverrideVariable" />, when it is set.</param>
    public static SteamAppTicketHelperCommand? Locate(string baseDirectory, string? processPath,
        Func<string, bool> exists, string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && exists(overridePath))
            return Command(overridePath);

        if (!string.IsNullOrWhiteSpace(processPath) &&
            string.Equals(Path.GetFileNameWithoutExtension(processPath), HelperName,
                StringComparison.OrdinalIgnoreCase))
            return new SteamAppTicketHelperCommand(processPath, Array.Empty<string>());

        foreach (var candidate in new[]
                 {
                     Path.Combine(baseDirectory, ExecutableName),
                     Path.Combine(baseDirectory, "cli", ExecutableName),
                     Path.Combine(baseDirectory, HelperName + ".dll"),
                     Path.Combine(baseDirectory, "cli", HelperName + ".dll")
                 })
            if (exists(candidate))
                return Command(candidate);

        return null;
    }

    private static SteamAppTicketHelperCommand Command(string path)
    {
        return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? new SteamAppTicketHelperCommand("dotnet", new[] {path})
            : new SteamAppTicketHelperCommand(path, Array.Empty<string>());
    }
}
