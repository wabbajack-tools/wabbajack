using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task SetNexusAPIKey(string key, long daily, long hourly)
        {
            await using var conn = await Open();
            await using var trans = await conn.BeginTransactionAsync();
            await conn.ExecuteAsync(@"DELETE FROM NexusKeys WHERE ApiKey = @ApiKey", new {ApiKey = key}, trans);
            await conn.ExecuteAsync(@"INSERT INTO NexusKeys (ApiKey, DailyRemain, HourlyRemain) VALUES (@ApiKey, @DailyRemain, @HourlyRemain)",
                new {ApiKey = key, DailyRemain = daily, HourlyRemain = hourly}, trans);
            await trans.CommitAsync();
        }

        
        public async Task DeleteNexusAPIKey(string key)
        {
            await using var conn = await Open();
            await conn.ExecuteAsync(@"DELETE FROM NexusKeys WHERE ApiKey = @ApiKey", new {ApiKey = key});
        }

        public async Task<List<string>> GetNexusApiKeys(int threshold = 1500)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<string>(@"SELECT ApiKey FROM NexusKeys WHERE DailyRemain >= @Threshold ORDER BY DailyRemain DESC",
                new {Threshold = threshold})).ToList();
        }

        public async Task<List<(string Key, int Daily, int Hourly)>> GetNexusApiKeysWithCounts(int threshold = 1500)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<(string, int, int)>(@"SELECT ApiKey, DailyRemain, HourlyRemain FROM NexusKeys WHERE DailyRemain >= @Threshold ORDER BY DailyRemain DESC",
                new {Threshold = threshold})).ToList();
        }

        
        public async Task<bool> HaveKey(string key)
        {
            await using var conn = await Open();
            return (await conn.QueryAsync<string>(@"SELECT ApiKey FROM NexusKeys WHERE ApiKey = @ApiKey",
                new {ApiKey = key})).Any();
        }

    }
}
