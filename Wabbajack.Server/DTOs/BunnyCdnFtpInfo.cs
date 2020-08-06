using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Server.DTOs
{
    public enum StorageSpace
    {
        AuthoredFiles,
        Patches,
        Mirrors
    }
    
    public class BunnyCdnFtpInfo
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Hostname { get; set; }

        public static async Task<BunnyCdnFtpInfo> GetCreds(StorageSpace space)
        {
            return (await Utils.FromEncryptedJson<Dictionary<string, BunnyCdnFtpInfo>>("bunnycdn"))[space.ToString()];
        }
    }
}
