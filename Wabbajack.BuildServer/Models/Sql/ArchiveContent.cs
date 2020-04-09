using System;
using System.Collections.Generic;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Model.Models
{
    public partial class ArchiveContent
    {
        public long Parent { get; set; }
        public long Child { get; set; }
        public RelativePath Path { get; set; }
        public byte[] PathHash { get; set; }
    }
}
