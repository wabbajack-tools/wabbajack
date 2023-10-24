using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Wabbajack.Paths;

namespace Wabbajack.BuildServer;

public class AppSettings
{
    public AppSettings(IConfiguration config)
    {
        config.Bind("WabbajackSettings", this);
    }
    public S3Settings ProxyStorage { get; set; }
}

public class S3Settings
{
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string ServiceURL { get; set; }
    
    public string BucketName { get; set; }
}