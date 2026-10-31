namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     Where an encrypted Steam app ticket comes from.
///     <para>
///         The ticket is a blob the running Steam client signs, stating who the account is and what it
///         owns. Bethesda decrypts it with a key only they hold and decides entitlement from it, which is
///         why this whole chain needs no Bethesda.net login: the ticket is the credential. Minting one
///         means talking to the local Steam client through the game's own native library, so it is kept
///         behind this seam - everything else here is ordinary HTTP and parsing, and stays testable
///         without Steam running.
///     </para>
/// </summary>
public interface ISteamAppTicketSource
{
    /// <summary>
    ///     Asks the running Steam client for an encrypted app ticket for <paramref name="appId" />. Throws
    ///     when Steam is not running, is not logged in, or never answers.
    /// </summary>
    ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default);
}
