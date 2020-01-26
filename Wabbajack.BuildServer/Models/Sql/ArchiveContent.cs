using System;
using System.Collections.Generic;

namespace Wabbajack.BuildServer.Model.Models
{
    public partial class ArchiveContent
    {
        public long Parent { get; set; }
        public long Child { get; set; }
        public string Path { get; set; }
        public byte[] PathHash { get; set; }
    }
}
