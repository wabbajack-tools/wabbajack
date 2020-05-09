using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        public async Task<string> LoginByApiKey(string key)
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<string>(@"SELECT Owner as Id FROM dbo.ApiKeys WHERE ApiKey = @ApiKey",
                new {ApiKey = key});
            return result.FirstOrDefault();
        }
        
        public async Task<string> AddLogin(string name)
        {
            var key = NewAPIKey();
            await using var conn = await Open();


            await conn.ExecuteAsync("INSERT INTO dbo.ApiKeys (Owner, ApiKey) VALUES (@Owner, @ApiKey)",
                new {Owner = name, ApiKey = key});
            return key;
        }
        
                
        public static string NewAPIKey()
        {
            var arr = new byte[128];
            new Random().NextBytes(arr);
            return arr.ToHex();
        }
        
        public async Task<IEnumerable<(string Owner, string Key)>> GetAllUserKeys()
        {
            await using var conn = await Open();
            var result = await conn.QueryAsync<(string Owner, string Key)>("SELECT Owner, ApiKey FROM dbo.ApiKeys");
            return result;
        }


    }
}
