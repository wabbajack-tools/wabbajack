using System;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.Models
{
    public class DownloadState
    {
        public string Key { get; set; }
        public Hash Hash { get; set; }
        
        public AbstractDownloadState State { get; set; }

        public bool IsValid { get; set; }
        public DateTime LastValidationTime { get; set; } = DateTime.Now;
        public DateTime FirstValidationTime { get; set; } = DateTime.Now;
    }
}
