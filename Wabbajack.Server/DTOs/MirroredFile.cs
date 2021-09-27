using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.DTOs
{
    public class MirroredFile
    {
        public Hash Hash { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Uploaded { get; set; }
        public string Rationale { get; set; }
        
        public string FailMessage { get; set; }

        public async Task Finish(SqlService sql)
        {
            Uploaded = DateTime.UtcNow;
            await sql.UpsertMirroredFile(this);
        }
        
        public async Task Fail(SqlService sql, string message)
        {
            Uploaded = DateTime.UtcNow;
            FailMessage = message;
            await sql.UpsertMirroredFile(this);
        }
    }
}
