using System;
using System.Threading.Tasks;
using Wabbajack.Lib;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.DTOs
{
    public class ArchiveDownload
    {
        public Guid Id { get; set; }
        public Archive Archive { get; set; }
        public bool? IsFailed { get; set; }
        public DateTime? DownloadFinished { get; set; }
        public string FailMessage { get; set; }

        public async Task Fail(SqlService service, string message)
        {
            IsFailed = true;
            DownloadFinished = DateTime.UtcNow;
            FailMessage = message;
            await service.UpdatePendingDownload(this);
        }

        public async Task Finish(SqlService service)
        {
            IsFailed = false;
            DownloadFinished = DateTime.UtcNow;
            await service.UpdatePendingDownload(this);
        }
    }
}
