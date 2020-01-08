using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.Models
{
    public class DownloadState
    {
        [BsonId]
        public string Key { get; set; }
        public string Hash { get; set; }
        
        public AbstractDownloadState State { get; set; }

        public bool IsValid { get; set; }
        public DateTime LastValidationTime { get; set; } = DateTime.Now;
        public DateTime FirstValidationTime { get; set; } = DateTime.Now;
    }
}
