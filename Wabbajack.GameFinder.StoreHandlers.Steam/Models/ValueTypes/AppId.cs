using System.Globalization;
using System.Web;
using JetBrains.Annotations;
using TransparentValueObjects;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;

/// <summary>
/// Represents a 32-bit unsigned integer unique identifier of an app.
/// </summary>
/// <example><c>262060</c></example>
[PublicAPI]
[ValueObject<uint>]
public readonly partial struct AppId : IAugmentWith<DefaultValueAugment>
{
    /// <inheritdoc/>
    public static AppId DefaultValue { get; } = From(0);

    /// <summary>
    /// Gets the URL to the SteamDB page of the app associated with this id.
    /// </summary>
    /// <example><c>https://steamdb.info/app/262060</c></example>
    public string GetSteamDbUrl() => $"{Constants.SteamDbBaseUrl}/app/{Value.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Gets the URL to the Steam Store page of the app associated with this id and with additional UTM parameters.
    /// </summary>
    /// <remarks>
    /// Setting the UTM source parameter helps developers identify what links to their app.
    /// See https://partner.steamgames.com/doc/marketing/utm_analytics for more details.
    /// </remarks>
    /// <param name="source">The current source. This should be the name of your app.</param>
    /// <returns></returns>
    /// <example>
    /// <c>https://store.steampowered.com/app/262060</c> or
    /// <c>https://store.steampowered.com/app/262060/?utm_source=MyApp</c>
    /// </example>
    public string GetSteamStoreUrl(string? source = null)
    {
        var url = $"{Constants.SteamStoreBaseUrl}/app/{Value.ToString(CultureInfo.InvariantCulture)}";
        return source is null ? url : $"{url}/?utm_source={HttpUtility.UrlEncode(source)}";
    }
}
