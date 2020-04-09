using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.Common;
using Wabbajack.BuildServer.Model.Models;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class ADBTest : IAsyncLifetime
    {
        private static string CONN_STR = @"Data Source=.\SQLEXPRESS;Integrated Security=True;";
        public string PublicConnStr => CONN_STR + $";Initial Catalog={DBName}";
        private AppSettings _appSettings;
        protected SqlService _sqlService;
        private bool _finishedSchema;
        private string DBName { get; }

        public ADBTest()
        {
            DBName = "test_db" + Guid.NewGuid().ToString().Replace("-", "_");
            User = Guid.NewGuid().ToString().Replace("-", "");
            APIKey = SqlService.NewAPIKey();
        }

        public string APIKey { get; }
        public string User { get; }

        public async Task InitializeAsync()
        {
            await CreateSchema();
        }
        
        private async Task CreateSchema()
        {
            Utils.Log("Creating Database");
            //var conn = new SqlConnection("Data Source=localhost,1433;User ID=test;Password=test;MultipleActiveResultSets=true");
            await using var conn = new SqlConnection(CONN_STR);

            await conn.OpenAsync();
            await KillTestDatabases(conn);
            //await new SqlCommand($"CREATE DATABASE {DBName};", conn).ExecuteNonQueryAsync();

            await using var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.BuildServer.Test.sql.wabbajack_db.sql");
            await using var ms = new MemoryStream();
            await schemaStream.CopyToAsync(ms);
            var schemaString = Encoding.UTF8.GetString(ms.ToArray()).Replace("wabbajack_prod", $"{DBName}");
            
            foreach (var statement in SplitSqlStatements(schemaString))
            {
                await new SqlCommand(statement, conn).ExecuteNonQueryAsync();
            }

            await new SqlCommand($"USE {DBName}", conn).ExecuteNonQueryAsync();
            
            await new SqlCommand($"INSERT INTO dbo.ApiKeys (APIKey, Owner) VALUES ('{APIKey}', '{User}');", conn).ExecuteNonQueryAsync();
            _finishedSchema = true;
        }

        private static IEnumerable<string> SplitSqlStatements(string sqlScript)
        {
            // Split by "GO" statements
            var statements = Regex.Split(
                sqlScript,
                @"^[\t \r\n]*GO[\t \r\n]*\d*[\t ]*(?:--.*)?$",
                RegexOptions.Multiline |
                RegexOptions.IgnorePatternWhitespace |
                RegexOptions.IgnoreCase);

            // Remove empties, trim, and return
            return statements
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim(' ', '\r', '\n'));
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            // Don't delete it if the setup failed, so we can debug the issue
            if (!_finishedSchema) return;
            Utils.Log("Deleting Database");
            await using  var conn = new SqlConnection(CONN_STR);

            await conn.OpenAsync();
            await KillTestDatabases(conn);
        }

        private async Task KillTestDatabases(SqlConnection conn)
        {
            await KillAll(conn);

            var dbs = await conn.QueryAsync<string>("SELECT name from [master].[sys].[databases]");

            foreach (var db in dbs.Where(name => name.StartsWith("test_")))
            {
                await new SqlCommand(
                        $"DROP DATABASE {db};",
                        conn)
                    .ExecuteNonQueryAsync();
            }
        }

        private async Task KillAll(SqlConnection conn)
        {
            await new SqlCommand($@"
                        DECLARE	@Spid INT
                        DECLARE	@ExecSQL VARCHAR(255)
                         
                        DECLARE	KillCursor CURSOR LOCAL STATIC READ_ONLY FORWARD_ONLY
                        FOR
                        SELECT	DISTINCT SPID
                        FROM	MASTER..SysProcesses
                        WHERE	DBID = DB_ID('{DBName}')
                         
                        OPEN	KillCursor
                         
                        -- Grab the first SPID
                        FETCH	NEXT
                        FROM	KillCursor
                        INTO	@Spid
                         
                        WHILE	@@FETCH_STATUS = 0
	                        BEGIN
		                        SET		@ExecSQL = 'KILL ' + CAST(@Spid AS VARCHAR(50))
                         
		                        EXEC	(@ExecSQL)
                         
		                        -- Pull the next SPID
                                FETCH	NEXT 
		                        FROM	KillCursor 
		                        INTO	@Spid  
	                        END
                         
                        CLOSE	KillCursor
                         
                        DEALLOCATE	KillCursor", conn).ExecuteNonQueryAsync();
        }

    }
}
