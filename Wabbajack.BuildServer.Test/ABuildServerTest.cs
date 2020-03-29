using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class ABuildServerTest : IAsyncLifetime
    {
        private static string CONN_STR = @"Data Source=.\SQLEXPRESS;Integrated Security=True;";
        private AppSettings _appSettings;
        protected SqlService _sqlService;
        private string DBName { get; }

        public ABuildServerTest(ITestOutputHelper helper)
        {
            TestContext = helper;
            DBName = "test_db" + Guid.NewGuid().ToString().Replace("-", "_");
            _appSettings = MakeAppSettings();
            _sqlService = new SqlService(_appSettings);
        }

        private AppSettings MakeAppSettings()
        {
            return new AppSettings
            {
                SqlConnection = CONN_STR + $"Initial Catalog={DBName}"
            };
        }

        public ITestOutputHelper TestContext { get;}

        public async Task InitializeAsync()
        {
            await CreateSchema();
        }
        
        private async Task CreateSchema()
        {
            TestContext.WriteLine("Creating Database");
            //var conn = new SqlConnection("Data Source=localhost,1433;User ID=test;Password=test;MultipleActiveResultSets=true");
            await using var conn = new SqlConnection(CONN_STR);

            await conn.OpenAsync();
            //await new SqlCommand($"CREATE DATABASE {DBName};", conn).ExecuteNonQueryAsync();

            await using var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.BuildServer.Test.sql.wabbajack_db.sql");
            await using var ms = new MemoryStream();
            await schemaStream.CopyToAsync(ms);
            var schemaString = Encoding.UTF8.GetString(ms.ToArray()).Replace("wabbajack_prod", $"{DBName}");
            
            foreach (var statement in SplitSqlStatements(schemaString))
            {
                await new SqlCommand(statement, conn).ExecuteNonQueryAsync();
                
            }
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
            TestContext.WriteLine("Deleting Database");
            await using  var conn = new SqlConnection(CONN_STR);

            await conn.OpenAsync();
            await KillAll(conn);
            await new SqlCommand($"DROP DATABASE {DBName};", conn).ExecuteNonQueryAsync();
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
