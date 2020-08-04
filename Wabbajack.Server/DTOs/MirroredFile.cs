using System;
using Wabbajack.Common;

namespace Wabbajack.Server.DTOs
{
    public class MirroredFile
    {
        public Hash Hash { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Uploaded { get; set; }
        public string Rationale { get; set; }
    }
}
