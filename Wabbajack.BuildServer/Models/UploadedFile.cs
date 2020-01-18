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

        [BsonIgnore]
        public string MungedName => $"{Path.GetFileNameWithoutExtension(Name)}-{Id}{Path.GetExtension(Name)}";

        [BsonIgnore] public object Uri => $"https://build.wabbajack.org/files/{MungedName}";

        public static async Task<UploadedFile> Ingest(DBContext db, IFormFile src, string uploader)
        {
            var record = new UploadedFile {Uploader = uploader, Name = src.FileName, Id = Guid.NewGuid().ToString()};
            var dest_path = $@"public\\files\\{record.MungedName}";

            using (var stream = File.OpenWrite(dest_path)) 
                await src.CopyToAsync(stream);
            record.Size = new FileInfo(dest_path).Length;
            record.Hash = await dest_path.FileHashAsync();
            await db.UploadedFiles.InsertOneAsync(record);
            return record;
        }
    }
}
