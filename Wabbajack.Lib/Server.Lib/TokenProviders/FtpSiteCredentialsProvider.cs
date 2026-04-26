using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Server.Lib.DTOs;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Server.Lib.TokenProviders;

public class FtpSiteCredentialsProvider : EncryptedJsonTokenProvider<Dictionary<StorageSpace, FtpSite>>,
    IFtpSiteCredentials
{
    public FtpSiteCredentialsProvider(ILogger<FtpSiteCredentialsProvider> logger, DTOSerializer dtos) :
        base(logger, dtos, "ftp-credentials")
    {
    }
}