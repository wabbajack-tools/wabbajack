using System;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<VirusScanner.Result?> FindVirusResult(Hash hash)
        {
            await using var conn = await Open();
            var results = await conn.QueryFirstOrDefaultAsync<(Hash, bool)>(
                @"SELECT Hash, IsMalware FROM dbo.VirusScanResults WHERE Hash = @Hash",
                new {Hash = hash});
            if (results == default)
                return null;
            return results.Item2 ? VirusScanner.Result.Malware : VirusScanner.Result.NotMalware;
        }
        
        public async Task AddVirusResult(Hash hash, VirusScanner.Result result)
        {
            await using var conn = await Open();
            try
            {
                var results = await conn.QueryFirstOrDefaultAsync<(Hash, bool)>(
                    @"INSERT INTO dbo.VirusScanResults (Hash, IsMalware) VALUES (@Hash, @IsMalware)",
                    new {Hash = hash, IsMalware = result == VirusScanner.Result.Malware});
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
