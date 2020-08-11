using System;
using System.Collections.Generic;
using System.Text;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Compression.BSA
{
    [JsonName("BSAFileState")]
    public class BSAFileStateObject : FileStateObject
    {
        public bool FlipCompression { get; set; }

        public BSAFileStateObject() { }

        public BSAFileStateObject(FileRecord fileRecord)
        {
            FlipCompression = fileRecord.FlipCompression;
            Path = fileRecord.Path;
            Index = fileRecord._index;
        }
    }
}
