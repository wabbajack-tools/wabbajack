using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Wabbajack.CacheServer.DTOs
{
    public class MongoDoc
    {
        public ObjectId _id { get; set; }  = ObjectId.Empty;
    }
}
