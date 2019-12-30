using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CacheServer.DTOs.JobQueue
{
    public class IndexJob : AJobPayload
    {
        public AbstractDownloadState State { get; set; }
        public override string Description { get; } = "Validate and index an archive";
        public override bool UsesNexus { get => State is NexusDownloader.State; }
        public override JobResult Execute()
        {
            throw new NotImplementedException();
        }
    }
}
