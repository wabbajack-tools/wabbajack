using System;
using Wabbajack.Common;

namespace Wabbajack.Server.DTOs
{
    public class Patch
    {
        public ArchiveDownload Src { get; set; }
        public ArchiveDownload Dest { get; set; }
        public Hash PatchHash { get; set; }
        public long PatchSize { get; set; }
        public DateTime? Finished { get; set; }
    }
}
