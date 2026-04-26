namespace Wabbajack.RateLimiter;

public record StatusReport(int Running, int Pending, long Transferred)
{
}