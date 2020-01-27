using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Wabbajack.BuildServer.Model.Models
{
    public class DbFactory
    {

        
        public static IDbConnection Connect()
        {
            return new SqlConnection(Configuration);
        }
    }
}
