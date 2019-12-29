using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CouchDB.Driver.Types;

namespace Wabbajack.CacheServer.DTOs
{
    public class Metric
    {
        public DateTime Timestamp;
        public string Action;
        public string Subject;
    }
}
