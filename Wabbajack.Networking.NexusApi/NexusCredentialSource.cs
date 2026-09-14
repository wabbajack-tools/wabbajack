namespace Wabbajack.Networking.NexusApi;

/// <summary>
///     Which credential <see cref="NexusApi" /> would authenticate its next call with. Read it through
///     <see cref="NexusApi.CredentialSource" />; it is resolved by exactly the code that builds the request
///     headers, so it cannot describe a credential the API would not actually use.
/// </summary>
public enum NexusCredentialSource
{
    /// <summary>Nothing to authenticate with.</summary>
    None,

    /// <summary>
    ///     An OAuth login: what the app stores when the user logs in, and what a host can hand it as
    ///     <c>NEXUS_OAUTH_INFO</c>. Refreshable, and tied to the account the user signed in as. Reported only
    ///     when the stored state carries an access token to send: a refresh Nexus refuses leaves one behind
    ///     that does not, and that is no more a login than an empty variable is.
    /// </summary>
    OAuth,

    /// <summary>A personal API key held in the same stored login state as <see cref="OAuth" />.</summary>
    StoredApiKey,

    /// <summary>
    ///     The <c>NEXUS_API_KEY</c> environment variable. <see cref="NexusApi" /> will make calls with it, but
    ///     no login is stored, so it is not a login as far as the download path is concerned - see
    ///     <see cref="NexusCredential.CanDownload" />.
    /// </summary>
    EnvironmentApiKey
}

public static class NexusCredential
{
    /// <summary>
    ///     The one definition of "logged in to Nexus Mods". <c>NexusDownloader.Prepare</c> and preflight's
    ///     Nexus login check both ask this and nothing else, so what the checklist claims and what the
    ///     download path can deliver cannot drift apart again.
    ///     <para>
    ///         <see cref="NexusCredentialSource.EnvironmentApiKey" /> is deliberately not a login.
    ///         <c>NEXUS_API_KEY</c> drives the raw API - the CLI's and the test suite's normal way in, and
    ///         what <c>NexusApi</c> falls back to - but the downloader needs a stored login: it reads the
    ///         OAuth state to decide whether to refresh before every download, and <c>ITokenProvider.Get</c>
    ///         throws when there is none. A stray key in a developer's environment therefore has to read as
    ///         logged out, or preflight promises Nexus downloads the installer will not make.
    ///     </para>
    /// </summary>
    public static bool CanDownload(this NexusCredentialSource source)
    {
        return source is NexusCredentialSource.OAuth or NexusCredentialSource.StoredApiKey;
    }
}
