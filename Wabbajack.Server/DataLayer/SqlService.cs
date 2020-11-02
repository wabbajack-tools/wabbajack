using System.Data.SqlClient;
using System.Threading.Tasks;
using Wabbajack.BuildServer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataLayer
{
    public partial class SqlService
    {
        private AppSettings _settings;
        private Task<BunnyCdnFtpInfo> _mirrorCreds;

        public SqlService(AppSettings settings)
        {
            _settings = settings;
            _mirrorCreds = BunnyCdnFtpInfo.GetCreds(StorageSpace.Mirrors);

        }

        public async Task<SqlConnection> Open()
        {
            var conn = new SqlConnection(_settings.SqlConnection);
            await conn.OpenAsync();
            return conn;
        }
    }
}
