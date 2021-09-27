using System.Collections.Generic;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.TokenProviders
{
    public interface IFtpSiteCredentials : ITokenProvider<Dictionary<StorageSpace, FtpSite>>
    {
        
    }
}