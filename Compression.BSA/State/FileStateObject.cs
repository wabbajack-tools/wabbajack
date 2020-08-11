using System;
using System.Collections.Generic;
using System.Text;
using Wabbajack.Common;

namespace Compression.BSA
{
    public abstract class FileStateObject
    {
        public int Index { get; set; }
        public RelativePath Path { get; set; }
    }
}
