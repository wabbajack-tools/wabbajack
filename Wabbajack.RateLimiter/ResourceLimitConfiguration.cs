namespace Wabbajack.RateLimiter;

public class ResourceLimitConfiguration
{
    public ResourceLimit Disk { get; init; } = new();
    public ResourceLimit CPU { get; init; } = new();
    public ResourceLimit Network { get; init; } = new();
}

public class ResourceLimit
{
    public int MaxConcurrentTasks { get; set; } = -1;
    public long MaxThroughput { get; set; } = -1;
}