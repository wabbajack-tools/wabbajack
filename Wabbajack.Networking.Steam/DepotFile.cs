namespace Wabbajack.Networking.Steam;

/// <summary>
///     One file as a depot manifest describes it, in terms that are ours rather than SteamKit's.
///     SteamKit's types stay inside this project -- that is the condition its licence was accepted under --
///     so anything a host needs to see about a depot file comes back as this.
/// </summary>
/// <param name="Path">The path the manifest records, depot-relative, with backslashes.</param>
/// <param name="Size">The file's size in bytes.</param>
/// <param name="Sha1">
///     The SHA-1 the manifest carries for the file's contents, upper-case hex, or empty when the manifest
///     recorded none. This is Valve's hash, written at build time; it is what a download is checked against
///     and it has nothing to do with the xxHash64 Wabbajack identifies archives by.
/// </param>
public record DepotFile(string Path, ulong Size, string Sha1);
