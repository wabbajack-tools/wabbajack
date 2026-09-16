namespace Wabbajack.Networking.Bethesda;

public interface ISteamAppTicketSource
{
    ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default);
}
