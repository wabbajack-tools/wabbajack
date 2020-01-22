using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson.Serialization.Attributes;
using Wabbajack.Common;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.BuildServer.Models
{
    public class UploadedFile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public string Uploader { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        
        public string CDNName { get; set; }

        [BsonIgnore]
        public string MungedName => $"{Path.GetFileNameWithoutExtension(Name)}-{Id}{Path.GetExtension(Name)}";

        [BsonIgnore]
        public string Uri => CDNName == null ? $"https://wabbajack.b-cdn.net/{MungedName}" : $"https://{CDNName}.b-cdn.net/{MungedName}";
    }
}
