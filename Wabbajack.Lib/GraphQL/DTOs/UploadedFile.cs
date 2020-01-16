using System;

namespace Wabbajack.Lib.GraphQL.DTOs
{
    public class UploadedFile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MungedName { get; set; }
        public DateTime UploadDate { get; set; }
        public string Uploader { get; set; }
        public Uri Uri { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
    }
}
