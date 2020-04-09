using System;
using Wabbajack.Common;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.BuildServer.Models
{
    public class UploadedFile
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public Hash Hash { get; set; }
        public string Uploader { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        
        public string CDNName { get; set; }

        public string MungedName => $"{Path.GetFileNameWithoutExtension(Name)}-{Id}{Path.GetExtension(Name)}";

        public string Uri => CDNName == null ? $"https://wabbajack.b-cdn.net/{MungedName}" : $"https://{CDNName}.b-cdn.net/{MungedName}";
    }
}
