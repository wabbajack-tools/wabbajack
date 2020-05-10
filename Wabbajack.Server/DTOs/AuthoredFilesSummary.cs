using System;

namespace Wabbajack.Server.DTOs
{
    public class AuthoredFilesSummary
    {
        public long Size { get; set; }
        public string OriginalFileName { get; set; }
        public string Author { get; set; }
        public DateTime LastTouched { get; set; }
        public DateTime? Finalized { get; set; }
        public string MungedName { get; set; }
        public string ServerAssignedUniqueId { get; set; }
    }
}
