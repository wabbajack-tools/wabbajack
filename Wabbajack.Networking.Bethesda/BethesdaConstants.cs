namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     The values the game sends to <c>api.bethesda.net</c>, all of them build-embedded constants rather
///     than anything per-user. They were recovered once, offline, from a decrypted <c>SkyrimSE.exe</c>
///     (build 1.6.1170) and are committed here; nothing at runtime reads the executable.
/// </summary>
public static class BethesdaConstants
{
    /// <summary>Everything in the chain except the <c>.ckm</c> payloads themselves lives under here.</summary>
    public static readonly Uri ApiBase = new("https://api.bethesda.net");

    /// <summary>
    ///     The <c>x-bnet-key</c> the edge checks before a request reaches the origin. A shared constant
    ///     baked into every copy of the game, sent verbatim; the server decodes it. It gates nothing but
    ///     the edge - ownership is decided by Bethesda decrypting the Steam ticket - so holding it buys no
    ///     content on its own.
    ///     <para>
    ///         Four 128-character candidates were recovered from the binary, one per Bethesda environment,
    ///         and this is the production one. It was settled with four probes against
    ///         <c>/session/external-login</c> carrying a deliberately garbage ticket, which needs no
    ///         credential of any kind, because the stack answers each class of key differently:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>a value the edge does not recognise (a random UUID) is blocked by CloudFront - HTTP 403, empty body;</item>
    ///         <item>the three non-production candidates reach the origin and are refused there - HTTP 404, <c>{"code":5001,"message":"Invalid Header values"}</c>, the same answer as sending no key header at all;</item>
    ///         <item>this key reaches the origin and gets as far as the ticket - HTTP 412, <c>{"code":14029,"message":"Error verifying authentication"}</c>, which is the garbage ticket being rejected and therefore the key being accepted.</item>
    ///     </list>
    ///     <para><c>BethesdaApiClientTests.OnlyTheProductionKeyReachesTicketVerification</c> re-runs those probes.</para>
    /// </summary>
    public const string ApiKey =
        "VVJnSFEJL3ovZ3JGd0F1XDEqYEo1XzA_N3FJR2tAfVBnRj9ZJjJkSUZdbHhS" +
        "CVRzNFc1DFZ8Y1kwMVc-eHBBMTZEQWZaYV1vTGc7dDBhXElpNVZSbFg5VUAz" +
        "VFdOJXU2";

    /// <summary>
    ///     <c>x-bnet-agent</c>. This is the game's version and not the SDK's, which is the one header value
    ///     the source protocol notes had wrong.
    /// </summary>
    public const string Agent = "1.6.1170.0";

    /// <summary><c>x-product</c>. <c>SKYRIM</c>, not <c>CREATIONCLUB</c>.</summary>
    public const string Product = "SKYRIM";

    /// <summary><c>x-platform</c>. The only platform this has been verified against.</summary>
    public const string Platform = "STEAM";

    /// <summary>
    ///     <c>user-agent</c>, on the API calls and on the <c>.ckm</c> fetches alike. The download URLs are
    ///     presigned and carry no auth of their own, but they do want this.
    /// </summary>
    public const string UserAgent = "bnet";

    /// <summary><c>x-country</c>, which the game fills from Steam's <c>GetIPCountry</c>.</summary>
    public const string DefaultCountry = "US";

    /// <summary><c>x-language</c>, and the <c>language</c> field of the login body.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>Skyrim Special Edition. The app the encrypted ticket has to be minted for.</summary>
    public const uint SkyrimSpecialEditionAppId = 489830;

    /// <summary>
    ///     The Anniversary Upgrade. Nothing here asks Steam about it - the ticket states what the account
    ///     owns and Bethesda reads that - but it is what an account has to hold for the resolve to return
    ///     anything.
    /// </summary>
    public const uint AnniversaryUpgradeAppId = 1746860;
}
