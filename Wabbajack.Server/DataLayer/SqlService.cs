using System.Data.SqlClient;
using System.Threading.Tasks;
using Wabbajack.BuildServer;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        private AppSettings _settings;

        public SqlService(AppSettings settings)
        {
            _settings = settings;

        }

        public async Task<SqlConnection> Open()
        {
            var conn = new SqlConnection(_settings.SqlConnection);
            await conn.OpenAsync();
            return conn;
        }
    }
}
