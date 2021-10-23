using System.Data.SqlClient;
using System.Threading.Tasks;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.Server.DataLayer;

public partial class SqlService
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly AppSettings _settings;

    public SqlService(AppSettings settings, DTOSerializer dtos, DownloadDispatcher dispatcher)
    {
        _settings = settings;
        _dtos = dtos;
        _dispatcher = dispatcher;
        // Ugly hack, but the SQL mappers need it
        _dtoStatic = dtos;
    }

    public async Task<SqlConnection> Open()
    {
        var conn = new SqlConnection(_settings.SqlConnection);
        await conn.OpenAsync();
        return conn;
    }
}