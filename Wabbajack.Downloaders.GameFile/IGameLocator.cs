using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

public interface IGameLocator
{
    public AbsolutePath GameLocation(Game game);
    public bool IsInstalled(Game game);
    public bool TryFindLocation(Game game, out AbsolutePath path);
    public bool TryGetSteamBuildId(Game game, out string buildId);

    /// <summary>
    ///     The depot and manifest ids Steam recorded for the copy of this game that is installed here, from
    ///     its <c>appmanifest_*.acf</c>.
    ///     <para>
    ///         This is the one place a manifest id for an <em>old</em> build can be had. Steam's client API
    ///         only ever says what a depot publishes now, so every historical id in
    ///         <c>indexed-game-files</c> came off a machine that had that build installed while it did -
    ///         which is what makes a user still sitting on 1.6.1170.0 the only source for it.
    ///     </para>
    /// </summary>
    public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests);
}