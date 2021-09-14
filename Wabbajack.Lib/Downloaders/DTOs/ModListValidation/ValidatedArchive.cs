using System;
using System.Text.Json.Serialization;

namespace Wabbajack.Lib.Downloaders.DTOs.ModListValidation
{
    public class ValidatedArchive
    {
        public ArchiveStatus Status { get; set; }
        public Archive Original { get; set; } = new(new HTTPDownloader.State("http://foo"));

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Archive? PatchedFrom { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Uri? PatchUrl { get; set; }
    }
}
