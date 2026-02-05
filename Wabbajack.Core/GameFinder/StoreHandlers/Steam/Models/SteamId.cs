using System;
using System.Globalization;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Unique identifier used to identify a Steam account.
/// </summary>
/// <remarks>
/// See https://developer.valvesoftware.com/wiki/SteamID for more information.
/// </remarks>
[PublicAPI]
public readonly struct SteamId : IEquatable<SteamId>, IComparable<SteamId>
{
    /// <summary>
    /// Represents an empty ID of an invalid user.
    /// </summary>
    public static readonly SteamId Empty = new(0);

    /// <summary>
    /// Compressed binary representation of the id.
    /// </summary>
    /// <example>76561198110222274</example>
    public readonly ulong RawId;

    /// <summary>
    /// Constructor using the compressed binary representation of the id.
    /// </summary>
    /// <param name="rawId">The raw 64-bit unique identifier.</param>
    public SteamId(ulong rawId)
    {
        RawId = rawId;
    }

    /// <summary>
    /// Factory method for consistency.
    /// </summary>
    /// <param name="rawId"></param>
    /// <returns></returns>
    public static SteamId From(ulong rawId) => new(rawId);

    /// <summary>
    /// Creates a new <see cref="SteamId"/> using an account Id.
    /// </summary>
    public static SteamId FromAccountId(
        uint accountId,
        SteamUniverse universe = SteamUniverse.Public,
        SteamAccountType accountType = SteamAccountType.Individual)
    {
        var rawId = (ulong)accountId;

        var universeMask = (ulong)universe << 56;
        var accountTypeMask = (ulong)accountType << 52;

        rawId |= universeMask;
        rawId |= accountTypeMask;

        return From(rawId);
    }

    /// <summary>
    /// Gets the universe of the account.
    /// </summary>
    public SteamUniverse Universe => (SteamUniverse)(int)(RawId >> 56);

    /// <summary>
    /// Gets the account type.
    /// </summary>
    public SteamAccountType AccountType => (SteamAccountType)((byte)(RawId >> 52) & 0xF);

    /// <summary>
    /// Gets the account identifier.
    /// </summary>
    /// <remarks>
    /// This identifier can be used to get the current user data in the Steam installation directory.
    /// It's also used by <see cref="Steam3Id"/>.
    /// </remarks>
    /// <example>149956546</example>
    public uint AccountId => (uint)(RawId << 32 >> 32);

    /// <summary>
    /// Gets the account number.
    /// </summary>
    /// <remarks>
    /// This is only useful for <see cref="Steam2Id"/>.
    /// </remarks>
    /// <example>74978273</example>
    public uint AccountNumber => (uint)(RawId << 32 >> 33);

    /// <summary>
    /// Gets the textually representation in the Steam2 ID format.
    /// </summary>
    /// <example>STEAM_1:0:74978273</example>
    /// <seealso cref="Steam3Id"/>
    public string Steam2Id => $"STEAM_{((byte)Universe).ToString(CultureInfo.InvariantCulture)}:{((byte)(RawId << 63 >> 63)).ToString(CultureInfo.InvariantCulture)}:{AccountNumber.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Gets the textually representation in the Steam3 ID format.
    /// </summary>
    /// <example>[U:1:149956546]</example>
    /// <seealso cref="Steam2Id"/>
    public string Steam3Id => $"[{AccountType.GetLetter()}:1:{AccountId.ToString(CultureInfo.InvariantCulture)}]";

    /// <summary>
    /// Gets the URL to the community profile page of the account using <see cref="RawId"/>.
    /// </summary>
    /// <example>https://steamcommunity.com/profiles/76561198110222274</example>
    public string GetProfileUrl() => $"{Constants.SteamCommunityBaseUrl}/profiles/{RawId}";

    /// <summary>
    /// Gets the URL to the community profile page of the account using the <see cref="Steam3Id"/>.
    /// </summary>
    /// <example>https://steamcommunity.com/profiles/[U:1:149956546]</example>
    public string GetSteam3IdProfileUrl() => $"{Constants.SteamCommunityBaseUrl}/profiles/{Steam3Id}";

    /// <inheritdoc/>
    public override string ToString() => Steam3Id;

    /// <inheritdoc/>
    public bool Equals(SteamId other) => RawId == other.RawId;

    /// <inheritdoc/>
    public int CompareTo(SteamId other) => RawId.CompareTo(other.RawId);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is SteamId other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => RawId.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SteamId a, SteamId b) => a.Equals(b);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SteamId a, SteamId b) => !a.Equals(b);
}
